# Compila Doom Companion y genera el instalador de Windows con Velopack.
#
# Uso (desde la raiz del repo):
#   powershell -ExecutionPolicy Bypass -File instalador\compilar-instalador.ps1 [-Version 1.0.0]
#
# Resultado: Releases\DoomCompanion-win-Setup.exe (instalador) y Releases\DoomCompanion-win-Portable.zip.

param(
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"
$raiz = Split-Path -Parent $PSScriptRoot
Set-Location $raiz

Write-Host "==> Tests" -ForegroundColor Cyan
dotnet test tests/DoomCompanion.Core.Tests -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw "Fallaron los tests: no se genera el instalador." }

Write-Host "==> Publicando (self-contained, win-x64)" -ForegroundColor Cyan
if (Test-Path publish) { Remove-Item -Recurse -Force publish }
dotnet publish src/DoomCompanion.App -c Release -r win-x64 --self-contained true `
    -p:Version=$Version -o publish --nologo
if ($LASTEXITCODE -ne 0) { throw "Fallo dotnet publish." }

Write-Host "==> Empaquetando con Velopack" -ForegroundColor Cyan
# La app no usa actualizaciones automaticas: no hace falta conservar releases anteriores,
# y Velopack se niega a empaquetar una version igual o menor a una ya existente.
if (Test-Path Releases) { Remove-Item -Recurse -Force Releases }
dotnet tool restore
dotnet vpk pack `
    --packId DoomCompanion `
    --runtime win-x64 `
    --packVersion $Version `
    --packDir publish `
    --mainExe DoomCompanion.exe `
    --packTitle "Doom Companion" `
    --packAuthors "Martin Glass" `
    --icon src/DoomCompanion.App/Recursos/icono.ico `
    --outputDir Releases
if ($LASTEXITCODE -ne 0) { throw "Fallo vpk pack." }

Write-Host ""
Write-Host "Listo. Instalador: $raiz\Releases\DoomCompanion-win-Setup.exe" -ForegroundColor Green

