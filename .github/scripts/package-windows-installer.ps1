param(
    [Parameter(Mandatory = $true)]
    [string]$Rid,

    [Parameter(Mandatory = $true)]
    [string]$RunNumber,

    [Parameter(Mandatory = $true)]
    [string]$RunAttempt
)

$ErrorActionPreference = 'Stop'

$publishDir = (Resolve-Path "./publish/$Rid").Path
$artifactDir = Join-Path (Resolve-Path ".").Path "artifacts/$Rid"
$outputBaseName = "$($env:APP_NAME)-$Rid-setup"
$appVersion = "1.0.$RunNumber.$RunAttempt"
$templatePath = (Resolve-Path "./.github/installer/windows/setup.iss").Path

New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null

$isccCandidates = @(
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe"
)
$iscc = $isccCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $iscc) {
    choco install innosetup --yes --no-progress
    $iscc = $isccCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
}

if (-not $iscc) {
    throw "ISCC.exe not found after installing Inno Setup"
}

$arguments = @(
    "/DMyAppName=$($env:APP_DISPLAY_NAME)",
    "/DMyAppVersion=$appVersion",
    "/DMyAppPublisher=$($env:APP_DISPLAY_NAME)",
    "/DMyAppExeName=$($env:APP_NAME).exe",
    "/DMyOutputDir=$artifactDir",
    "/DMyOutputBaseName=$outputBaseName",
    "/DMySourceDir=$publishDir",
    $templatePath
)

& $iscc @arguments
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup compiler failed with exit code $LASTEXITCODE"
}

$installerPath = Join-Path $artifactDir "$outputBaseName.exe"
if (-not (Test-Path $installerPath)) {
    throw "Expected installer was not generated: $installerPath"
}
