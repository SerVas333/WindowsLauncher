# ===== Deploy-FirebirdDatabase.ps1 =====
# PowerShell скрипт для автоматизации развертывания Firebird БД v1.0.0.001
# Поддерживает Embedded и Server режимы

param(
    [Parameter(Mandatory=$true)]
    [ValidateSet("Embedded", "Server")]
    [string]$Mode,
    
    [Parameter(Mandatory=$false)]
    [string]$ServerHost = "localhost",
    
    [Parameter(Mandatory=$false)]
    [int]$ServerPort = 3050,
    
    [Parameter(Mandatory=$false)]
    [string]$DatabasePath,
    
    [Parameter(Mandatory=$false)]
    [string]$SysDBaPassword,
    
    [Parameter(Mandatory=$false)]
    [string]$AppUserPassword,
    
    [Parameter(Mandatory=$false)]
    [switch]$CreateBackup,
    
    [Parameter(Mandatory=$false)]
    [switch]$Verbose
)

# Цвета для вывода
$ErrorColor = "Red"
$SuccessColor = "Green" 
$InfoColor = "Cyan"
$WarningColor = "Yellow"

function Write-ColoredOutput {
    param([string]$Message, [string]$Color = "White")
    Write-Host $Message -ForegroundColor $Color
}

function Test-FirebirdInstallation {
    Write-ColoredOutput "Проверка установки Firebird..." $InfoColor
    
    $isqlPath = @(
        "C:\Program Files\Firebird\Firebird_3_0\bin\isql.exe",
        "C:\Program Files\Firebird\Firebird_4_0\bin\isql.exe", 
        "C:\Program Files (x86)\Firebird\Firebird_3_0\bin\isql.exe",
        "C:\Program Files (x86)\Firebird\Firebird_4_0\bin\isql.exe",
		"C:\Program Files (x86)\Firebird\Firebird_5_0\isql.exe"
    ) | Where-Object { Test-Path $_ } | Select-Object -First 1
    
    if (-not $isqlPath) {
        Write-ColoredOutput "ОШИБКА: isql.exe не найден. Установите Firebird." $ErrorColor
        return $null
    }
    
    Write-ColoredOutput "Найден isql: $isqlPath" $SuccessColor
    return $isqlPath
}

function Generate-SecurePassword {
    $characters = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789!@#$%^&*"
    $password = ""
    for ($i = 0; $i -lt 16; $i++) {
        $password += $characters[(Get-Random -Maximum $characters.Length)]
    }
    return "KDV_$password"
}

function Deploy-EmbeddedDatabase {
    param([string]$IsqlPath, [string]$DbPath, [string]$Password)
    
    Write-ColoredOutput "Развертывание Firebird Embedded БД..." $InfoColor
    
    # Создаем директорию если не существует
    $dbDir = Split-Path $DbPath -Parent
    if (-not (Test-Path $dbDir)) {
        New-Item -ItemType Directory -Path $dbDir -Force | Out-Null
        Write-ColoredOutput "Создана директория: $dbDir" $InfoColor
    }
    
    # Создаем скрипт создания БД
    $createScript = @"
CREATE DATABASE '$DbPath' 
PAGE_SIZE 8192
USER 'SYSDBA' 
PASSWORD '$Password'
DEFAULT CHARACTER SET UTF8;
QUIT;
"@
    
    $scriptPath = Join-Path $env:TEMP "create_embedded_db.sql"
    $createScript | Out-File -FilePath $scriptPath -Encoding UTF8
    
    try {
        # Создаем БД
        Write-ColoredOutput "Создание embedded базы данных..." $InfoColor
        & $IsqlPath -i $scriptPath
        
        if ($LASTEXITCODE -eq 0) {
            Write-ColoredOutput "База данных создана успешно" $SuccessColor
            
            # Применяем схему
            $schemaScript = ".\create_firebird_embedded_v1.0.0.001.sql"
            if (Test-Path $schemaScript) {
                Write-ColoredOutput "Применение схемы БД..." $InfoColor
                & $IsqlPath -user SYSDBA -password $Password -i $schemaScript $DbPath
                
                if ($LASTEXITCODE -eq 0) {
                    Write-ColoredOutput "Схема применена успешно" $SuccessColor
                } else {
                    Write-ColoredOutput "ОШИБКА: Не удалось применить схему" $ErrorColor
                }
            }
        } else {
            Write-ColoredOutput "ОШИБКА: Не удалось создать базу данных" $ErrorColor
        }
    }
    finally {
        Remove-Item $scriptPath -Force -ErrorAction SilentlyContinue
    }
}

function Deploy-ServerDatabase {
    param([string]$IsqlPath, [string]$Server, [int]$Port, [string]$DbPath, [string]$SysPassword, [string]$AppPassword)
    
    Write-ColoredOutput "Развертывание Firebird Server БД..." $InfoColor
    
    $connectionString = "$Server/$Port`:$DbPath"
    
    # Создаем пользователя приложения
    Write-ColoredOutput "Создание пользователя приложения..." $InfoColor
    $userScript = @"
CONNECT '$Server/$Port`:security3.fdb' USER 'SYSDBA' PASSWORD '$SysPassword';
CREATE USER KDV_LAUNCHER PASSWORD '$AppPassword';
ALTER USER KDV_LAUNCHER GRANT ADMIN ROLE;
QUIT;
"@
    
    $userScriptPath = Join-Path $env:TEMP "create_user.sql"
    $userScript | Out-File -FilePath $userScriptPath -Encoding UTF8
    
    try {
        & $IsqlPath -i $userScriptPath
        Write-ColoredOutput "Пользователь создан" $SuccessColor
    }
    catch {
        Write-ColoredOutput "ПРЕДУПРЕЖДЕНИЕ: Возможно пользователь уже существует" $WarningColor
    }
    finally {
        Remove-Item $userScriptPath -Force -ErrorAction SilentlyContinue  
    }
    
    # Создаем БД
    Write-ColoredOutput "Создание server базы данных..." $InfoColor
    $createScript = @"
CREATE DATABASE '$connectionString'
PAGE_SIZE 16384
USER 'SYSDBA' 
PASSWORD '$SysPassword'
DEFAULT CHARACTER SET UTF8;
QUIT;
"@
    
    $createScriptPath = Join-Path $env:TEMP "create_server_db.sql"
    $createScript | Out-File -FilePath $createScriptPath -Encoding UTF8
    
    try {
        & $IsqlPath -i $createScriptPath
        
        if ($LASTEXITCODE -eq 0) {
            Write-ColoredOutput "База данных создана успешно" $SuccessColor
            
            # Применяем схему
            $schemaScript = ".\create_firebird_server_v1.0.0.001.sql"
            if (Test-Path $schemaScript) {
                Write-ColoredOutput "Применение схемы БД..." $InfoColor
                
                # Заменяем пароли в схеме
                $schemaContent = Get-Content $schemaScript -Raw
                $schemaContent = $schemaContent -replace "your_sysdba_password", $SysPassword
                $schemaContent = $schemaContent -replace "KDV_L@unch3r_S3cur3_2025!", $AppPassword
                
                $tempSchemaPath = Join-Path $env:TEMP "schema_temp.sql"
                $schemaContent | Out-File -FilePath $tempSchemaPath -Encoding UTF8
                
                & $IsqlPath -i $tempSchemaPath
                
                if ($LASTEXITCODE -eq 0) {
                    Write-ColoredOutput "Схема применена успешно" $SuccessColor
                } else {
                    Write-ColoredOutput "ОШИБКА: Не удалось применить схему" $ErrorColor
                }
                
                Remove-Item $tempSchemaPath -Force -ErrorAction SilentlyContinue
            }
        } else {
            Write-ColoredOutput "ОШИБКА: Не удалось создать базу данных" $ErrorColor
        }
    }
    finally {
        Remove-Item $createScriptPath -Force -ErrorAction SilentlyContinue
    }
}

function Create-DatabaseConfig {
    param([string]$Mode, [string]$Server, [int]$Port, [string]$DbPath, [string]$Username, [string]$Password)
    
    Write-ColoredOutput "Создание конфигурации приложения..." $InfoColor
    
    $config = @{
        DatabaseType = "Firebird"
        ConnectionMode = $Mode
        ConnectionTimeout = 30
    }
    
    if ($Mode -eq "Server") {
        $config.Server = $Server
        $config.Port = $Port
        $config.DatabasePath = $DbPath
    } else {
        $config.DatabasePath = $DbPath
    }
    
    $config.Username = $Username
    $config.Password = $Password
    
    $configPath = ".\database-config.json"
    $config | ConvertTo-Json -Depth 3 | Out-File -FilePath $configPath -Encoding UTF8
    
    Write-ColoredOutput "Конфигурация сохранена: $configPath" $SuccessColor
    
    # Показываем конфигурацию (без пароля)
    $displayConfig = $config.Clone()
    $displayConfig.Password = "***СКРЫТ***"
    Write-ColoredOutput "Конфигурация:" $InfoColor
    $displayConfig | ConvertTo-Json -Depth 3 | Write-Host
}

function Create-Backup {
    param([string]$IsqlPath, [string]$DbPath, [string]$Username, [string]$Password)
    
    if (-not $CreateBackup) { return }
    
    Write-ColoredOutput "Создание backup БД..." $InfoColor
    
    $gbakPath = $IsqlPath -replace "isql\.exe", "gbak.exe"
    if (-not (Test-Path $gbakPath)) {
        Write-ColoredOutput "ПРЕДУПРЕЖДЕНИЕ: gbak.exe не найден" $WarningColor
        return
    }
    
    $backupPath = $DbPath -replace "\.fdb$", "_backup_$(Get-Date -Format 'yyyyMMdd_HHmmss').fbk"
    
    try {
        & $gbakPath -b -user $Username -password $Password $DbPath $backupPath
        
        if ($LASTEXITCODE -eq 0) {
            Write-ColoredOutput "Backup создан: $backupPath" $SuccessColor
        } else {
            Write-ColoredOutput "ОШИБКА: Не удалось создать backup" $ErrorColor
        }
    }
    catch {
        Write-ColoredOutput "ОШИБКА backup: $($_.Exception.Message)" $ErrorColor
    }
}

# ===== ОСНОВНАЯ ЛОГИКА =====

try {
    Write-ColoredOutput "=== Deploy Firebird Database v1.0.0.001 ===" $InfoColor
    Write-ColoredOutput "Режим: $Mode" $InfoColor
    
    # Проверяем установку Firebird
    $isqlPath = Test-FirebirdInstallation
    if (-not $isqlPath) { exit 1 }
    
    # Генерируем пароли если не указаны
    if (-not $SysDBaPassword) {
        $SysDBaPassword = Generate-SecurePassword
        Write-ColoredOutput "Сгенерирован пароль SYSDBA: $SysDBaPassword" $WarningColor
    }
    
    if (-not $AppUserPassword) {
        $AppUserPassword = Generate-SecurePassword  
        Write-ColoredOutput "Сгенерирован пароль приложения: $AppUserPassword" $WarningColor
    }
    
    # Устанавливаем пути по умолчанию
    if (-not $DatabasePath) {
        if ($Mode -eq "Embedded") {
            $DatabasePath = "C:\WindowsLauncher\Data\launcher_embedded.fdb"
        } else {
            $DatabasePath = "C:\FirebirdData\WindowsLauncher\launcher_server.fdb"
        }
    }
    
    Write-ColoredOutput "Путь к БД: $DatabasePath" $InfoColor
    
    # Развертываем БД
    if ($Mode -eq "Embedded") {
        Deploy-EmbeddedDatabase -IsqlPath $isqlPath -DbPath $DatabasePath -Password $SysDBaPassword
        Create-DatabaseConfig -Mode "Embedded" -DbPath $DatabasePath -Username "SYSDBA" -Password $SysDBaPassword
        Create-Backup -IsqlPath $isqlPath -DbPath $DatabasePath -Username "SYSDBA" -Password $SysDBaPassword
    } else {
        Deploy-ServerDatabase -IsqlPath $isqlPath -Server $ServerHost -Port $ServerPort -DbPath $DatabasePath -SysPassword $SysDBaPassword -AppPassword $AppUserPassword
        Create-DatabaseConfig -Mode "Server" -Server $ServerHost -Port $ServerPort -DbPath $DatabasePath -Username "KDV_LAUNCHER" -Password $AppUserPassword
        Create-Backup -IsqlPath $isqlPath -DbPath "$ServerHost/$ServerPort`:$DatabasePath" -Username "KDV_LAUNCHER" -Password $AppUserPassword
    }
    
    Write-ColoredOutput "=== РАЗВЕРТЫВАНИЕ ЗАВЕРШЕНО УСПЕШНО ===" $SuccessColor
    
    Write-ColoredOutput "`nСледующие шаги:" $InfoColor
    Write-ColoredOutput "1. Скопируйте database-config.json в папку приложения" $InfoColor
    Write-ColoredOutput "2. Запустите приложение для тестирования подключения" $InfoColor
    Write-ColoredOutput "3. Сохраните пароли в безопасном месте" $InfoColor
    
    if ($Mode -eq "Server") {
        Write-ColoredOutput "4. Настройте файрвол для порта $ServerPort" $InfoColor
        Write-ColoredOutput "5. Настройте backup стратегию" $InfoColor
    }
}
catch {
    Write-ColoredOutput "КРИТИЧЕСКАЯ ОШИБКА: $($_.Exception.Message)" $ErrorColor
    exit 1
}

<#
.SYNOPSIS
Автоматизированное развертывание Firebird базы данных для WindowsLauncher v1.0.0.001

.DESCRIPTION
Скрипт автоматизирует процесс создания и настройки Firebird базы данных
в режимах Embedded или Server с применением схемы v1.0.0.001

.PARAMETER Mode
Режим развертывания: Embedded или Server

.PARAMETER ServerHost  
Хост сервера Firebird (для режима Server)

.PARAMETER ServerPort
Порт сервера Firebird (по умолчанию 3050)

.PARAMETER DatabasePath
Путь к файлу базы данных

.PARAMETER SysDBaPassword
Пароль пользователя SYSDBA (генерируется автоматически если не указан)

.PARAMETER AppUserPassword
Пароль пользователя приложения (генерируется автоматически если не указан)

.PARAMETER CreateBackup
Создать backup после развертывания

.EXAMPLE
.\Deploy-FirebirdDatabase.ps1 -Mode Embedded

.EXAMPLE  
.\Deploy-FirebirdDatabase.ps1 -Mode Server -ServerHost "fb-server.company.local" -CreateBackup

.EXAMPLE
.\Deploy-FirebirdDatabase.ps1 -Mode Server -DatabasePath "/opt/firebird/launcher.fdb" -SysDBaPassword "MySecurePassword"
#>