$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$buildScript = Join-Path $projectRoot "build.ps1"
$installerScript = Join-Path $projectRoot "installer\LuluDesktopPet.iss"
$compilerCandidates = @(
    (Join-Path $projectRoot "tools\InnoSetup7\ISCC.exe"),
    "C:\Program Files\Inno Setup 7\ISCC.exe",
    "C:\Program Files (x86)\Inno Setup 7\ISCC.exe",
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe"
)

$compiler = $compilerCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $compiler) {
    throw "Inno Setup compiler was not found. Install Inno Setup 7 and run this script again."
}

& powershell -NoProfile -ExecutionPolicy Bypass -File $buildScript
if ($LASTEXITCODE -ne 0) {
    throw "Application build failed with exit code: $LASTEXITCODE"
}

& $compiler $installerScript
if ($LASTEXITCODE -ne 0) {
    throw "Installer build failed with exit code: $LASTEXITCODE"
}

Write-Host "Installer build complete."
