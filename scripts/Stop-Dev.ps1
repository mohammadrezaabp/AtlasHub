$ErrorActionPreference='Stop'
Set-Location -LiteralPath (Split-Path -Parent $PSScriptRoot)
if (Test-Path 'data/dev-processes.json') {
    Get-Content 'data/dev-processes.json' -Raw | ConvertFrom-Json | ForEach-Object {
        $taskProcess=Get-Process -Id $_ -ErrorAction SilentlyContinue
        if ($taskProcess -and $taskProcess.ProcessName -eq 'dotnet') { Stop-Process -Id $taskProcess.Id }
    }
    Remove-Item -LiteralPath 'data/dev-processes.json'
}
