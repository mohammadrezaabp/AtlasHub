param([switch]$InstallHook)
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath (Split-Path -Parent $PSScriptRoot)

if ($InstallHook) {
    & git config core.hooksPath .githooks
    if ($LASTEXITCODE -ne 0) { throw 'Could not enable repository hooks.' }
    Write-Host 'Repository hooks enabled.'
}

$paths = @(& git -c core.quotepath=false ls-files --cached)
if ($LASTEXITCODE -ne 0) { throw 'Run this check from an initialized Git repository.' }
if ($paths.Count -eq 0) { throw 'No files are staged or tracked.' }

$localSecrets = @{}
if (Test-Path -LiteralPath '.env') {
    foreach ($line in Get-Content -LiteralPath '.env') {
        if ($line -match '^([A-Z0-9_]*(?:KEY|TOKEN|PASSWORD)[A-Z0-9_]*)=(.+)$') {
            $value = $Matches[2].Trim().Trim('"').Trim("'")
            if ($value.Length -ge 12 -and $value -notmatch '^replace-with-') { $localSecrets[$Matches[1]] = $value }
        }
    }
}

$blocked = @()
foreach ($path in $paths) {
    if (($path -match '(^|/)(\.env(?:\..*)?|\.envrc)$' -and $path -ne '.env.example') -or
        $path -match '^(data|artifacts|\.tools|secrets|\.secrets)/|(^|/)(bin|obj|TestResults|node_modules)/' -or
        ($path -like 'knowledge/*' -and $path -ne 'knowledge/.gitkeep') -or
        $path -match '(?i)\.(pem|key|pfx|p12|dump|bak|zip|db(?:-wal|-shm)?)$') {
        $blocked += "Private or generated file: $path"
        continue
    }
    $content = (& git show ":$path") -join "`n"
    if ($LASTEXITCODE -ne 0) { throw "Cannot inspect indexed file: $path" }
    if ($content -match 'sk-or-v1-[a-fA-F0-9]{32,}|gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{30,}|-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----') {
        $blocked += "Credential pattern: $path"
    }
    foreach ($name in $localSecrets.Keys) {
        if ($content.Contains($localSecrets[$name])) { $blocked += "Local credential ($name) found in: $path" }
    }
}
if ($blocked.Count -gt 0) {
    $blocked | ForEach-Object { Write-Host $_ -ForegroundColor Red }
    throw 'Publication blocked. Remove sensitive files or values from the Git index.'
}
Write-Host "Publication safety check passed for $($paths.Count) indexed files."
