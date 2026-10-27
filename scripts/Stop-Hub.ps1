$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath (Split-Path -Parent $PSScriptRoot)
docker compose --profile observability down
Write-Host 'Atlas stopped. Database, uploads, and other persistent volumes are retained.'
