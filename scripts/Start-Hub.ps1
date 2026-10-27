param([switch]$Observability, [switch]$Prebuilt)
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath (Split-Path -Parent $PSScriptRoot)
if (-not (Test-Path -LiteralPath '.env')) { throw 'Create .env from .env.example and configure your credentials first.' }
docker info --format '{{.ServerVersion}}' | Out-Null
if ($LASTEXITCODE -ne 0) { docker desktop start; if ($LASTEXITCODE -ne 0) { throw 'Start Docker Desktop, then run this script again.' } }
$taskArgs = @('compose')
if ($Prebuilt) {
    $taskSdk = if (Test-Path '.tools/dotnet/dotnet.exe') { (Resolve-Path '.tools/dotnet/dotnet.exe').Path } else { (Get-Command dotnet).Source }
    foreach ($taskProject in @('Hub.Web','Hub.Gateway','Hub.Tools','Hub.Worker')) {
        & $taskSdk publish "src/$taskProject/$taskProject.csproj" -c Release -o "artifacts/publish/$taskProject" --nologo -v minimal
        if ($LASTEXITCODE -ne 0) { throw "Publish failed: $taskProject" }
    }
    $taskArgs += @('-f','compose.yaml','-f','compose.prebuilt.yaml')
}
if ($Observability) { $env:OTEL_EXPORTER_OTLP_ENDPOINT='http://otel:4317'; $taskArgs += @('--profile','observability') }
$taskArgs += @('up','-d','--build')
& docker @taskArgs
if ($LASTEXITCODE -ne 0) { throw 'Docker startup failed. Inspect docker compose logs.' }
$taskPort = ((Get-Content '.env' | Where-Object { $_ -match '^HUB_PORT=' }) -replace '^HUB_PORT=','')
if (-not $taskPort) { $taskPort = '5080' }
Write-Host "Atlas is starting at http://localhost:$taskPort"
Write-Host 'Your initial administrator credentials are in the local .env file.'
