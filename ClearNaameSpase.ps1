# PowerShell скрипт для автоматической очистки пространств имен
# Запускать из корневой папки проекта

Write-Host "🔍 Начинаю аудит пространств имен..." -ForegroundColor Yellow

# 1. Найти все дублирующиеся классы
Write-Host "`n📋 Поиск дублирующихся классов..." -ForegroundColor Cyan

$duplicateClasses = @()
$classes = Get-ChildItem -Recurse -Filter "*.cs" | ForEach-Object {
    $content = Get-Content $_.FullName -Raw
    if ($content -match 'public class (\w+)') {
        $className = $matches[1]
        [PSCustomObject]@{
            ClassName = $className
            File = $_.FullName
            Namespace = if ($content -match 'namespace ([\w\.]+)') { $matches[1] } else { "Unknown" }
        }
    }
}

$groupedClasses = $classes | Group-Object ClassName
foreach ($group in $groupedClasses) {
    if ($group.Count -gt 1) {
        Write-Host "❌ Дублирующийся класс: $($group.Name)" -ForegroundColor Red
        foreach ($item in $group.Group) {
            Write-Host "   📁 $($item.Namespace) → $($item.File)" -ForegroundColor Gray
        }
        $duplicateClasses += $group.Name
    }
}

if ($duplicateClasses.Count -eq 0) {
    Write-Host "✅ Дублирующихся классов не найдено" -ForegroundColor Green
}

# 2. Проверить соответствие namespace и папок
Write-Host "`n📂 Проверка соответствия namespace и папок..." -ForegroundColor Cyan

$namespaceIssues = @()
Get-ChildItem -Recurse -Filter "*.cs" | ForEach-Object {
    $content = Get-Content $_.FullName -Raw
    if ($content -match 'namespace ([\w\.]+)') {
        $actualNamespace = $matches[1]
        
        # Вычисляем ожидаемый namespace на основе пути
        $relativePath = $_.DirectoryName -replace [regex]::Escape($PWD.Path), ""
        $relativePath = $relativePath.TrimStart('\')
        $expectedNamespace = $relativePath -replace '\\', '.'
        
        if ($expectedNamespace -and $actualNamespace -ne $expectedNamespace) {
            $issue = [PSCustomObject]@{
                File = $_.FullName
                Actual = $actualNamespace
                Expected = $expectedNamespace
            }
            $namespaceIssues += $issue
            Write-Host "❌ $($_.Name)" -ForegroundColor Red
            Write-Host "   Текущий:   $actualNamespace" -ForegroundColor Gray
            Write-Host "   Ожидаемый: $expectedNamespace" -ForegroundColor Gray
        }
    }
}

if ($namespaceIssues.Count -eq 0) {
    Write-Host "✅ Все namespace соответствуют структуре папок" -ForegroundColor Green
}

# 3. Найти отсутствующие using директивы
Write-Host "`n📝 Проверка using директив..." -ForegroundColor Cyan

$missingUsings = @()
Get-ChildItem -Recurse -Filter "*.cs" | ForEach-Object {
    $content = Get-Content $_.FullName -Raw
    
    # Проверяем наличие базовых using
    $requiredUsings = @(
        "System",
        "System.Collections.Generic", 
        "System.Threading.Tasks"
    )
    
    foreach ($using in $requiredUsings) {
        if ($content -match "\b($using\b)" -and $content -notmatch "using $using;") {
            $missingUsings += [PSCustomObject]@{
                File = $_.Name
                MissingUsing = $using
            }
        }
    }
}

# 4. Генерация отчета
Write-Host "`n📊 Генерация отчета..." -ForegroundColor Cyan

$report = @"
# Отчет по пространствам имен
Дата: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')

## Дублирующиеся классы ($($duplicateClasses.Count))
$($duplicateClasses | ForEach-Object { "- $_" } | Out-String)

## Проблемы с namespace ($($namespaceIssues.Count))
$($namespaceIssues | ForEach-Object { "- $($_.File): $($_.Actual) → $($_.Expected)" } | Out-String)

## Отсутствующие using ($($missingUsings.Count))
$($missingUsings | ForEach-Object { "- $($_.File): $($_.MissingUsing)" } | Out-String)
"@

$report | Out-File "namespace_audit_report.md" -Encoding UTF8
Write-Host "📄 Отчет сохранен в namespace_audit_report.md" -ForegroundColor Green

# 5. Предложения по исправлению
Write-Host "`n🔧 Рекомендации по исправлению:" -ForegroundColor Yellow

if ($duplicateClasses.Count -gt 0) {
    Write-Host "1. Удалить дублирующиеся классы:" -ForegroundColor White
    foreach ($className in $duplicateClasses) {
        Write-Host "   - Оставить $className только в одном месте" -ForegroundColor Gray
    }
}

if ($namespaceIssues.Count -gt 0) {
    Write-Host "2. Исправить namespace в следующих файлах:" -ForegroundColor White
    foreach ($issue in $namespaceIssues) {
        Write-Host "   - $($issue.File -replace [regex]::Escape($PWD.Path), '.')" -ForegroundColor Gray
    }
}

Write-Host "`n✨ Аудит завершен!" -ForegroundColor Green