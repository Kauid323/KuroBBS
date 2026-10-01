# KuroBBS Windows Phone 8.1 Metro Client Build Script
param(
    [string]$Configuration = "Debug",
    [string]$Platform = "AnyCPU" # AnyCPU, ARM, x86
)

$msbuild = "C:\Program Files (x86)\MSBuild\12.0\Bin\MSBuild.exe"
$projectPath = "$PSScriptRoot\KuroBBS\KuroBBS.WindowsPhone\KuroBBS.WindowsPhone.csproj"

Write-Host "=================================================" -ForegroundColor Cyan
Write-Host " Building KuroBBS for Windows Phone 8.1 (Metro) " -ForegroundColor Cyan
Write-Host " Config: $Configuration | Platform: $Platform" -ForegroundColor Cyan
Write-Host "=================================================" -ForegroundColor Cyan

& $msbuild $projectPath /p:Configuration=$Configuration /p:Platform=$Platform /v:m

if ($LASTEXITCODE -eq 0) {
    Write-Host "`n[SUCCESS] Build completed! Appx packages located at:" -ForegroundColor Green
    Get-ChildItem -Path "$PSScriptRoot\KuroBBS\KuroBBS.WindowsPhone\AppPackages" -Recurse -Filter "*.appx" | Select-Object FullName, Length
} else {
    Write-Host "`n[FAILED] Build failed with exit code $LASTEXITCODE" -ForegroundColor Red
}
