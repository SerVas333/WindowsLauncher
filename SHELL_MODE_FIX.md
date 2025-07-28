# Исправление режима Shell - возврат к LoginWindow

## Проблема

При использовании приложения в качестве Shell для Windows, после закрытия MainWindow происходил переход к LoginWindow, но при повторном входе не происходило открытие MainWindow.

### Симптомы:
- Первый вход работает нормально
- После logout/закрытия показывается LoginWindow 
- При повторном вводе учетных данных аутентификация проходит успешно
- Но MainWindow не открывается
- В логах видно: "User logged out successfully" после аутентификации

## Причина проблемы

В коде App.xaml.cs был жестко закодирован обработчик закрытия MainWindow:

```csharp
mainWindow.Closed += (s, e) => 
{
    logger.LogInformation("MainWindow closed, shutting down application");
    Shutdown(0); // ❌ Всегда завершал приложение
};
```

Это не учитывало конфигурацию Shell режима из `appsettings.json`:

```json
"SessionManagement": {
  "RunAsShell": true,
  "ReturnToLoginOnLogout": true,
  ...
}
```

## Решение

### 1. **Новый обработчик закрытия MainWindow**

```csharp
mainWindow.Closed += async (s, e) => 
{
    await HandleMainWindowClosedAsync(logger);
};
```

### 2. **Метод HandleMainWindowClosedAsync**

Добавлен новый метод, который проверяет конфигурацию и принимает решение:

```csharp
private async Task HandleMainWindowClosedAsync(ILogger<App> logger)
{
    // Получаем конфигурацию сессии
    var sessionManager = ServiceProvider?.GetService<ISessionManagementService>();
    await sessionManager.LoadConfigurationAsync();
    var config = sessionManager.Configuration;

    if (config.RunAsShell && config.ReturnToLoginOnLogout)
    {
        // Shell режим: возвращаемся к окну входа
        MainWindow = null;
        ShowLoginWindow();
    }
    else
    {
        // Обычный режим: завершаем приложение
        Shutdown(0);
    }
}
```

### 3. **Улучшенный ShowLoginWindow**

Обновлен метод для правильной обработки Shell режима:

```csharp
private async void ShowLoginWindow()
{
    var result = loginWindow.ShowDialog();

    if (result == true && loginWindow.AuthenticatedUser != null)
    {
        // Успешная аутентификация
        ShowMainWindow(loginWindow.AuthenticatedUser);
    }
    else
    {
        // Проверяем Shell режим
        if (isShellMode)
        {
            // В Shell режиме показываем LoginWindow снова
            await Task.Delay(500);
            ShowLoginWindow();
        }
        else
        {
            // В обычном режиме завершаем
            Shutdown(0);
        }
    }
}
```

## Конфигурация Shell режима

В `appsettings.json` можно настроить поведение:

```json
"SessionManagement": {
  "RunAsShell": true,                    // Работать как Shell
  "AutoRestartOnClose": true,            // Автоперезапуск при закрытии
  "LogoutOnMainWindowClose": true,       // Logout при закрытии MainWindow
  "ReturnToLoginOnLogout": true,         // Возврат к LoginWindow после logout
  "AllowMultipleSessions": false,        // Запретить множественные сессии
  "ShellWarningMessage": "...",          // Сообщение предупреждения
  "LogoutConfirmationMessage": "...",    // Сообщение подтверждения logout
  "MinimizeInsteadOfClose": false        // Минимизировать вместо закрытия
}
```

## Тестирование исправления

### Сценарий 1: Shell режим

1. **Запуск:** Показывается LoginWindow
2. **Вход:** Аутентификация → показывается MainWindow
3. **Закрытие MainWindow:** Автоматически показывается LoginWindow
4. **Повторный вход:** Аутентификация → показывается MainWindow ✅
5. **Logout кнопка:** Показывается LoginWindow
6. **Повторный вход:** Снова работает ✅

### Сценарий 2: Обычный режим

1. **Запуск:** LoginWindow
2. **Вход:** MainWindow
3. **Закрытие MainWindow:** Приложение завершается ✅
4. **Logout кнопка:** Приложение завершается ✅

## Логирование

Теперь в логах будет видно правильное поведение:

**Shell режим:**
```
[Information] MainWindow closed, checking shell mode configuration
[Information] Shell mode configuration - RunAsShell: True, ReturnToLoginOnLogout: True
[Information] Shell mode: returning to login window
[Information] Shell mode: login cancelled, showing login window again
```

**Обычный режим:**
```
[Information] MainWindow closed, checking shell mode configuration
[Information] Shell mode configuration - RunAsShell: False, ReturnToLoginOnLogout: False
[Information] Standard mode: shutting down application
```

## Дополнительные улучшения

### Безопасность в Shell режиме

- Невозможно закрыть приложение стандартными средствами
- При отмене входа снова показывается LoginWindow
- Нет способа "выйти" из Shell без перезагрузки

### Предотвращение зависания

- Добавлена задержка `Task.Delay(500)` перед повторным показом
- Сброс `MainWindow = null` для избежания конфликтов
- Обработка исключений во всех сценариях

### Совместимость

- Работает как в Shell режиме, так и в обычном
- Сохраняется существующее поведение для обычных пользователей
- Новое поведение активируется только при `RunAsShell: true`

---

**Статус:** ✅ Исправлено  
**Тестирование:** 🧪 Готово к проверке  
**Совместимость:** ✅ Обратная совместимость сохранена