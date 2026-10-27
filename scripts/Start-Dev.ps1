param([switch]$SkipBuild)
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath (Split-Path -Parent $PSScriptRoot)
if (-not (Test-Path '.env')) { throw 'Create .env from .env.example first.' }
$taskConfig = @{}
Get-Content '.env' | ForEach-Object { if ($_ -match '^([^#=]+)=(.*)$') { $taskConfig[$Matches[1]] = $Matches[2] } }
$taskSdk = if (Test-Path '.tools/dotnet/dotnet.exe') { (Resolve-Path '.tools/dotnet/dotnet.exe').Path } else { (Get-Command dotnet).Source }
New-Item -ItemType Directory -Force -Path 'data','data/uploads','data/keys','artifacts' | Out-Null
$env:Database__Provider='Sqlite'
$env:ConnectionStrings__Hub="Data Source=$PWD/data/atlas.db"
$env:Security__KeyPath="$PWD/data/keys"
$env:Storage__Path="$PWD/data/uploads"
$env:Storage__Provider='Local'
$env:Seed__Path="$PWD/knowledge"
$env:Gateway__Url='http://localhost:5081'
$env:Tools__Url='http://localhost:5082'
$env:Security__InternalToken=$taskConfig.INTERNAL_TOKEN
$env:Bootstrap__Email=$taskConfig.ADMIN_EMAIL
$env:Bootstrap__Password=$taskConfig.ADMIN_PASSWORD
$env:OpenRouter__ApiKey=$taskConfig.OPENROUTER_API_KEY
$env:Deepgram__ApiKey=$taskConfig.DEEPGRAM_API_KEY
$env:Budget__DailyUsd=$taskConfig.DAILY_BUDGET_USD
$env:Budget__MaxRequestUsd=$taskConfig.MAX_REQUEST_USD
$env:Models__Chat=$taskConfig.CHAT_MODEL
$env:Models__Fast=$taskConfig.FAST_MODEL
$env:Models__Rerank=$taskConfig.RERANK_MODEL
$env:Models__Vision=$taskConfig.VISION_MODEL
$env:Models__Embedding=$taskConfig.EMBEDDING_MODEL
$env:Models__Fallback=$taskConfig.FALLBACK_MODEL
if (-not $SkipBuild) {
    & $taskSdk build AtlasHub.slnx --nologo -v minimal
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
}
$taskProcesses=@()
foreach ($taskService in @(@('Hub.Web',5080),@('Hub.Gateway',5081),@('Hub.Tools',5082),@('Hub.Worker',5083))) {
    $taskAssembly = (Resolve-Path "src/$($taskService[0])/bin/Debug/net10.0/$($taskService[0]).dll").Path
    $env:ASPNETCORE_URLS="http://localhost:$($taskService[1])"
    $taskProcess=Start-Process -FilePath $taskSdk -ArgumentList @('"'+$taskAssembly+'"') -WorkingDirectory "$PWD/src/$($taskService[0])" -WindowStyle Hidden -PassThru -RedirectStandardOutput "$PWD/artifacts/$($taskService[0]).log" -RedirectStandardError "$PWD/artifacts/$($taskService[0]).error.log"
    $taskProcesses += $taskProcess.Id
}
$taskProcesses | ConvertTo-Json | Set-Content 'data/dev-processes.json'
Write-Host 'Atlas development workspace: http://localhost:5080'
Write-Host 'This mode uses SQLite and persisted vectors; Docker mode uses PostgreSQL, Redis and Qdrant.'
