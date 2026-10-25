# Установка всего нужного для разработки Zombie Survival на Windows.
# Запуск: правый клик по файлу -> "Выполнить с помощью PowerShell"
# или в PowerShell:  powershell -ExecutionPolicy Bypass -File .\setup-windows.ps1
#
# Что делает:
#   1. Ставит Git, Unity Hub и VS Code (через winget).
#   2. Создаёт папку "ZombieSurvival" на рабочем столе и скачивает туда проект.
#   3. Ставит Unity 6.3 LTS (6000.3.25f1) через Unity Hub.

$ErrorActionPreference = "Stop"

$UnityVersion   = "6000.3.25f1"
$UnityChangeset = "e1dba0a9aba4"
$RepoUrl        = "https://github.com/TrolikZalomaiev/onlinemenu.git"
$Branch         = "claude/zombie-survival-coop-game-mnobkz"

$Desktop    = [Environment]::GetFolderPath("Desktop")
$ProjectDir = Join-Path $Desktop "ZombieSurvival"

function Install-WingetPackage($id) {
    Write-Host "==> Установка $id" -ForegroundColor Cyan
    winget install --id $id -e --accept-source-agreements --accept-package-agreements --silent
}

if (-not (Get-Command winget -ErrorAction SilentlyContinue)) {
    Write-Host "winget не найден. Установи 'App Installer' из Microsoft Store и запусти скрипт снова." -ForegroundColor Red
    exit 1
}

Install-WingetPackage "Git.Git"
Install-WingetPackage "Unity.UnityHub"
Install-WingetPackage "Microsoft.VisualStudioCode"

# Обновляем PATH в текущем окне, чтобы git был виден сразу.
$env:Path = [Environment]::GetEnvironmentVariable("Path", "Machine") + ";" + [Environment]::GetEnvironmentVariable("Path", "User")

# --- Проект на рабочем столе ---
if (Test-Path (Join-Path $ProjectDir "Assets")) {
    Write-Host "==> Проект уже есть в $ProjectDir, пропускаю скачивание." -ForegroundColor Yellow
} else {
    Write-Host "==> Скачиваю проект в $ProjectDir" -ForegroundColor Cyan
    $Temp = Join-Path $env:TEMP "zs-clone"
    if (Test-Path $Temp) { Remove-Item $Temp -Recurse -Force }
    git clone --depth 1 --branch $Branch $RepoUrl $Temp
    New-Item -ItemType Directory -Force -Path $ProjectDir | Out-Null
    Copy-Item -Path (Join-Path $Temp "ZombieSurvival\*") -Destination $ProjectDir -Recurse -Force
    Copy-Item -Path (Join-Path $Temp "ZombieSurvival\.*") -Destination $ProjectDir -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item $Temp -Recurse -Force
}

# --- Unity Editor ---
$Hub = "C:\Program Files\Unity Hub\Unity Hub.exe"
if (Test-Path $Hub) {
    Write-Host "==> Ставлю Unity $UnityVersion (это надолго, ~5-10 ГБ)..." -ForegroundColor Cyan
    & $Hub -- --headless install --version $UnityVersion --changeset $UnityChangeset --module windows-il2cpp
} else {
    Write-Host "Unity Hub не найден по пути $Hub. Открой Unity Hub и установи $UnityVersion вручную." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Готово!" -ForegroundColor Green
Write-Host "1. Открой Unity Hub -> Add -> Add project from disk -> $ProjectDir"
Write-Host "2. В Unity: меню Zombie Survival -> 2. Собрать прототип сцены"
Write-Host "3. Нажми Play -> 'Создать игру (хост)'"
