# ===== Setup-TestSmtp.ps1 - Настройка тестовых SMTP серверов =====
# Этот скрипт добавляет тестовые SMTP настройки в базу данных WindowsLauncher
# ВНИМАНИЕ: Только для разработки и тестирования!

param(
    [Parameter(Mandatory=$false)]
    [string]$DatabasePath = "$env:APPDATA\WindowsLauncher\launcher.db",
    
    [Parameter(Mandatory=$false)]
    [string]$DatabaseType = "SQLite",
    
    [Parameter(Mandatory=$false)]
    [string]$GmailUsername = "test.launcher@gmail.com",
    
    [Parameter(Mandatory=$false)]
    [string]$GmailPassword = "your_app_password_here",
    
    [Parameter(Mandatory=$false)]
    [string]$OutlookUsername = "test.launcher@outlook.com",
    
    [Parameter(Mandatory=$false)]
    [string]$OutlookPassword = "your_app_password_here",
    
    [Parameter(Mandatory=$false)]
    [switch]$Force
)

Write-Host "===== WindowsLauncher SMTP Test Setup =====" -ForegroundColor Green
Write-Host "Database: $DatabasePath" -ForegroundColor Yellow
Write-Host "Type: $DatabaseType" -ForegroundColor Yellow

# Проверяем наличие базы данных
if (-not (Test-Path $DatabasePath)) {
    Write-Error "База данных не найдена: $DatabasePath"
    Write-Host "Запустите приложение сначала для создания базы данных" -ForegroundColor Red
    exit 1
}

# Предупреждение о безопасности
if (-not $Force) {
    Write-Warning "ВНИМАНИЕ: Этот скрипт добавит тестовые SMTP настройки в базу данных!"
    Write-Warning "Используйте только для разработки и тестирования!"
    Write-Host ""
    $confirm = Read-Host "Продолжить? (y/N)"
    if ($confirm -ne "y" -and $confirm -ne "Y") {
        Write-Host "Отменено пользователем" -ForegroundColor Yellow
        exit 0
    }
}

# Проверяем пароли
if ($GmailPassword -eq "your_app_password_here" -or $OutlookPassword -eq "your_app_password_here") {
    Write-Error "Пожалуйста, укажите реальные пароли приложений"
    Write-Host "Использование:" -ForegroundColor Yellow
    Write-Host "  .\Setup-TestSmtp.ps1 -GmailPassword 'abcd-efgh-ijkl-mnop' -OutlookPassword 'wxyz-1234-5678-90ab'" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Как получить пароли приложений:" -ForegroundColor Cyan
    Write-Host "Gmail: https://myaccount.google.com/apppasswords" -ForegroundColor Cyan
    Write-Host "Outlook: https://account.live.com/proofs/AppPassword" -ForegroundColor Cyan
    exit 1
}

Write-Host "Добавление тестовых SMTP настроек..." -ForegroundColor Green

try {
    # Подключение к SQLite (требует sqlite3.exe в PATH или установленного SQLite)
    if ($DatabaseType -eq "SQLite") {
        # Проверяем наличие sqlite3
        $sqliteCmd = Get-Command sqlite3 -ErrorAction SilentlyContinue
        if (-not $sqliteCmd) {
            Write-Error "sqlite3.exe не найден в PATH"
            Write-Host "Установите SQLite или добавьте его в PATH" -ForegroundColor Red
            exit 1
        }
        
        # Создаем временный SQL файл
        $tempSql = [System.IO.Path]::GetTempFileName() + ".sql"
        
        $sqlContent = @"
-- Добавляем Gmail SMTP (Primary)
INSERT OR IGNORE INTO SMTP_SETTINGS (
    HOST, PORT, USERNAME, ENCRYPTED_PASSWORD, USE_SSL, USE_STARTTLS, 
    SERVER_TYPE, DEFAULT_FROM_EMAIL, DEFAULT_FROM_NAME, IS_ACTIVE, 
    CONSECUTIVE_ERRORS, CREATED_AT
) VALUES (
    'smtp.gmail.com', 587, '$GmailUsername', '$GmailPassword', 0, 1, 0,
    '$GmailUsername', 'Windows Launcher Primary', 1, 0, datetime('now')
);

-- Добавляем Outlook SMTP (Backup)  
INSERT OR IGNORE INTO SMTP_SETTINGS (
    HOST, PORT, USERNAME, ENCRYPTED_PASSWORD, USE_SSL, USE_STARTTLS,
    SERVER_TYPE, DEFAULT_FROM_EMAIL, DEFAULT_FROM_NAME, IS_ACTIVE,
    CONSECUTIVE_ERRORS, CREATED_AT
) VALUES (
    'smtp-mail.outlook.com', 587, '$OutlookUsername', '$OutlookPassword', 0, 1, 1,
    '$OutlookUsername', 'Windows Launcher Backup', 1, 0, datetime('now')
);

-- Проверяем результат
SELECT 'SMTP Settings Added:' as Status;
SELECT ID, HOST, PORT, USERNAME, SERVER_TYPE, DEFAULT_FROM_NAME, IS_ACTIVE 
FROM SMTP_SETTINGS 
ORDER BY SERVER_TYPE;
"@
        
        Set-Content -Path $tempSql -Value $sqlContent -Encoding UTF8
        
        Write-Host "Выполнение SQL команд..." -ForegroundColor Yellow
        & sqlite3 $DatabasePath ".read `"$tempSql`""
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ SMTP настройки успешно добавлены!" -ForegroundColor Green
            Write-Host ""
            Write-Host "Настроенные серверы:" -ForegroundColor Cyan
            Write-Host "  Primary (Gmail): $GmailUsername" -ForegroundColor White  
            Write-Host "  Backup (Outlook): $OutlookUsername" -ForegroundColor White
        } else {
            Write-Error "Ошибка выполнения SQL команд"
            exit 1
        }
        
        # Удаляем временный файл
        Remove-Item $tempSql -Force -ErrorAction SilentlyContinue
    }
    else {
        Write-Error "Поддерживается только SQLite база данных"
        exit 1
    }
}
catch {
    Write-Error "Ошибка настройки SMTP: $($_.Exception.Message)"
    exit 1
}

Write-Host ""
Write-Host "===== Готово! =====" -ForegroundColor Green
Write-Host "Теперь вы можете тестировать email функциональность в Windows Launcher" -ForegroundColor White
Write-Host ""
Write-Host "ВАЖНО: В производственной среде используйте зашифрованные пароли!" -ForegroundColor Red
Write-Host "Для этого используйте AdminWindow → SMTP Settings" -ForegroundColor Yellow