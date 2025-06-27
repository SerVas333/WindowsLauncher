# clean-build.ps1
Write-Host "Cleaning WindowsLauncher solution..." -ForegroundColor Yellow

# Закрыть процессы
Write-Host "Stopping MSBuild processes..."
Get-Process MSBuild -ErrorAction SilentlyContinue | Stop-Process -Force

# Очистить решение
Write-Host "Running dotnet clean..."
dotnet clean

# Удалить bin и obj
Write-Host "Removing bin and obj folders..."
Get-ChildItem -Path . -Include bin,obj -Recurse | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

# Удалить .vs
Write-Host "Removing .vs folder..."
Remove-Item -Path .\.vs -Recurse -Force -ErrorAction SilentlyContinue

# Удалить user файлы
Write-Host "Removing .user files..."
Get-ChildItem -Path . -Include *.user -Recurse | Remove-Item -Force -ErrorAction SilentlyContinue

# Очистить кэш NuGet
Write-Host "Clearing NuGet cache..."
dotnet nuget locals all --clear

# Восстановить пакеты
Write-Host "Restoring packages..." -ForegroundColor Green
dotnet restore

# Пересобрать
Write-Host "Building solution..." -ForegroundColor Green
dotnet build --configuration Debug --no-incremental

Write-Host "Clean build completed!" -ForegroundColor Green