$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$compiler = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$framework = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319"
$wpf = Join-Path $framework "WPF"
$outputDir = Join-Path $projectRoot "dist"
$assetDir = Join-Path $outputDir "assets"

if (-not (Test-Path -LiteralPath $compiler)) {
    throw "Windows C# compiler not found: $compiler"
}

New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
New-Item -ItemType Directory -Force -Path $assetDir | Out-Null

& $compiler `
    /nologo `
    /target:winexe `
    /platform:anycpu `
    /optimize+ `
    /out:"$outputDir\LuluDesktopPet.exe" `
    /win32icon:"$projectRoot\assets\lulu.ico" `
    /reference:"$wpf\PresentationCore.dll" `
    /reference:"$wpf\PresentationFramework.dll" `
    /reference:"$wpf\WindowsBase.dll" `
    /reference:"$framework\System.Xaml.dll" `
    /reference:"$framework\System.Web.Extensions.dll" `
    "$projectRoot\src\LuluDesktopPet.cs"

if ($LASTEXITCODE -ne 0) {
    throw "Build failed with exit code: $LASTEXITCODE"
}

Copy-Item -LiteralPath "$projectRoot\assets\lulu-typing.png" -Destination "$assetDir\lulu-typing.png" -Force
Copy-Item -LiteralPath "$projectRoot\assets\lulu-typing-press.png" -Destination "$assetDir\lulu-typing-press.png" -Force
Copy-Item -LiteralPath "$projectRoot\assets\lulu.ico" -Destination "$assetDir\lulu.ico" -Force
Write-Host "Build complete: $outputDir\LuluDesktopPet.exe"
