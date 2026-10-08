param(
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$outputPath = Join-Path $projectRoot "artifacts\VoiceToText-$Runtime"

dotnet publish (Join-Path $projectRoot "VoiceToText.csproj") `
    --configuration Release `
    --runtime $Runtime `
    --self-contained true `
    --output $outputPath

Write-Host "`nГотово: $outputPath" -ForegroundColor Green
