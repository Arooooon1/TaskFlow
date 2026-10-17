$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
$taskFlowLocalSdk = Join-Path $PSScriptRoot '.tools/dotnet/dotnet.exe'
if (Test-Path -LiteralPath $taskFlowLocalSdk) {
    $taskFlowDotnet = $taskFlowLocalSdk
    $env:DOTNET_ROOT = Split-Path -Parent $taskFlowLocalSdk
    $env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot '.tools'
    $env:NUGET_PACKAGES = Join-Path $PSScriptRoot '.tools/nuget'
} else { $taskFlowDotnet = 'dotnet' }
& $taskFlowDotnet test TaskFlow.sln --logger trx --results-directory artifacts/test-results
exit $LASTEXITCODE
