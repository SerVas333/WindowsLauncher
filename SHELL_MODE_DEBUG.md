# Диагностика проблемы Shell Mode - повторный вход

## Проблема

После исправления Shell Mode логики, первый вход работает корректно, но при повторном входе MainWindow не открывается. В логах видно:

```
[Information] Guest authentication successful for user: guest
[Information] User logged out successfully: guest
```

Аутентификация проходит успешно, но сразу происходит logout.

## Анализ проблемы

### 1. **Конфликт сессий в SessionManagementService**

В `appsettings.json` установлено:
```json
"SessionManagement": {
  "AllowMultipleSessions": false
}
```

При повторном входе:
1. Старая сессия может быть еще активной (`_isSessionActive = true`)
2. При `StartSessionAsync()` срабатывает проверка множественных сессий
3. Вызывается `EndSessionAsync("Multiple sessions not allowed")`
4. Это генерирует событие logout

### 2. **Дублирование logout операций**

Возможны два источника logout:
1. `MainViewModel.Logout()` через кнопку или команду
2. `HandleMainWindowClosedAsync()` при закрытии окна

## Исправления

### 1. **Обновлена логика множественных сессий**

В `SessionManagementService.StartSessionAsync()`:

```csharp
// Если это тот же пользователь, просто обновляем сессию без завершения
if (_currentUser?.Username == user.Username)
{
    _logger.LogInformation("Same user re-login, updating existing session instead of ending");
    _currentUser = user; // Обновляем данные пользователя
    return true;
}
```

**Логика:** Если тот же пользователь входит повторно, не завершаем сессию, а обновляем ее.

### 2. **Явное завершение сессии в Shell режиме**

В `App.HandleMainWindowClosedAsync()`:

```csharp
// ВАЖНО: Явно завершаем сессию перед переходом к LoginWindow
await sessionManager.EndSessionAsync("MainWindow closed in shell mode");
logger.LogInformation("Session ended successfully");

// Небольшая задержка для гарантии завершения всех операций
await Task.Delay(200);
```

**Логика:** Гарантируем полное завершение сессии перед переходом к LoginWindow.

## Диагностика

### Проверка логов

При корректной работе логи должны показывать:

**1. Первый вход:**
```
[Information] Starting session for user: guest (Standard)
[Information] Session started for user guest with role Standard
[Information] Main window shown for user guest
```

**2. Закрытие MainWindow в Shell режиме:**
```
[Information] MainWindow closed, checking shell mode configuration
[Information] Shell mode: ending current session and returning to login window
[Information] Ending session for user: guest, reason: MainWindow closed in shell mode
[Information] Session ended successfully
```

**3. Повторный вход:**
```
[Information] Starting session for user: guest (Standard)
[Information] Same user re-login, updating existing session instead of ending
[Information] Main window shown for user guest
```

### Проблемные логи

Если проблема остается, логи покажут:

```
[Information] Starting session for user: guest (Standard)
[Warning] Multiple sessions not allowed, ending current session for: guest, new user: guest
[Information] Ending session for user: guest, reason: Multiple sessions not allowed
[Information] User logged out successfully: guest
```

## Дополнительные проверки

### 1. **Проверить вызовы Logout()**

Добавить breakpoint или логи в `MainViewModel.Logout()`:

```csharp
Logger.LogInformation("Logout() method called from: {StackTrace}", 
    Environment.StackTrace);
```

### 2. **Проверить состояние сессии**

В `SessionManagementService.StartSessionAsync()` добавить диагностику:

```csharp
_logger.LogDebug("Session state before start: Active={IsActive}, CurrentUser={User}", 
    _isSessionActive, _currentUser?.Username ?? "null");
```

### 3. **Проверить события сессии**

Если есть подписчики на события `SessionEventArgs`, они могут вызывать дополнительные операции.

## Временное решение

Если проблема персистирует, можно временно установить:

```json
"SessionManagement": {
  "AllowMultipleSessions": true
}
```

Это позволит избежать конфликтов сессий, но может создать другие проблемы.

## Следующие шаги

1. **Тестирование исправлений:** Проверить работу с новой логикой
2. **Анализ логов:** Убедиться, что логи соответствуют ожидаемым
3. **Дополнительная диагностика:** Если проблема остается, добавить больше логирования

---

**Статус:** 🔧 В процессе исправления  
**Приоритет:** 🔥 Критический - блокирует Shell режим  
**Ожидаемый результат:** Повторный вход должен открывать MainWindow