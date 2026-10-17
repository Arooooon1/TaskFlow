$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
$taskFlowLocalSdk = Join-Path $PSScriptRoot '.tools/dotnet/dotnet.exe'
if (Test-Path -LiteralPath $taskFlowLocalSdk) {
    $taskFlowDotnet = $taskFlowLocalSdk
    $env:DOTNET_ROOT = Split-Path -Parent $taskFlowLocalSdk
    $env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot '.tools'
    $env:NUGET_PACKAGES = Join-Path $PSScriptRoot '.tools/nuget'
} else {
    $taskFlowDotnet = 'dotnet'
}
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
Write-Host 'TaskFlow: http://127.0.0.1:5080'
Write-Host 'Use Ctrl+C to stop. Data is stored in src/TaskFlow.Api/App_Data.'
& $taskFlowDotnet run --project src/TaskFlow.Api --urls http://127.0.0.1:5080
exit $LASTEXITCODE
