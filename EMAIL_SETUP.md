# Email Система - Руководство по настройке

## Обзор

WindowsLauncher включает полнофункциональную email систему с поддержкой:

- 📧 **Отправка сообщений** через SMTP с поддержкой основного и резервного серверов
- 📇 **Адресная книга** с импортом/экспортом CSV
- 📎 **Вложения** до 25 МБ
- 🔐 **Шифрование паролей** для безопасного хранения
- 🔄 **Fallback система** - автоматическое переключение на резервный SMTP при сбоях

## Компоненты системы

### Backend
- `EmailService` - основной сервис отправки с поддержкой MailKit
- `SmtpSettingsRepository` - управление SMTP настройками
- `AddressBookService` - управление контактами
- `EncryptionService` - шифрование паролей SMTP

### UI Components  
- `ComposeEmailWindow` - создание и отправка сообщений
- `AddressBookWindow` - управление контактами
- `ContactEditWindow` - редактирование контактов

### База данных
- `CONTACTS` - таблица контактов
- `SMTP_SETTINGS` - настройки SMTP серверов
- Миграция `v1.1.0.001_AddEmailSupport` автоматически создает структуру

## Быстрая настройка для разработки

### 1. Создание тестовых email аккаунтов

**Gmail:**
1. Создайте тестовый Gmail аккаунт: `test.launcher@gmail.com`
2. Включите двухфакторную аутентификацию
3. Создайте App Password: https://myaccount.google.com/apppasswords
4. Сохраните сгенерированный пароль (формат: `abcd-efgh-ijkl-mnop`)

**Outlook:**
1. Создайте тестовый Outlook аккаунт: `test.launcher@outlook.com`
2. Включите двухфакторную аутентификацию  
3. Создайте App Password: https://account.live.com/proofs/AppPassword
4. Сохраните сгенерированный пароль

### 2. Автоматическая настройка (PowerShell)

```powershell
# Перейдите в папку scripts
cd "C:\WindowsLauncher\scripts"

# Выполните настройку с вашими паролями приложений
.\Setup-TestSmtp.ps1 -GmailPassword "abcd-efgh-ijkl-mnop" -OutlookPassword "wxyz-1234-5678-90ab"
```

Скрипт автоматически:
- Добавит Gmail как основной SMTP сервер
- Добавит Outlook как резервный SMTP сервер
- Покажет результат настройки

### 3. Ручная настройка через UI

1. Запустите WindowsLauncher как Administrator
2. Откройте Admin Panel (🛠️)
3. Перейдите в раздел "SMTP Settings"
4. Добавьте серверы вручную:

**Primary SMTP (Gmail):**
```
Host: smtp.gmail.com
Port: 587
Username: test.launcher@gmail.com  
Password: [используйте зашифрованный пароль]
Use STARTTLS: ✅
Server Type: Primary
```

**Backup SMTP (Outlook):**
```
Host: smtp-mail.outlook.com
Port: 587
Username: test.launcher@outlook.com
Password: [используйте зашифрованный пароль]
Use STARTTLS: ✅
Server Type: Backup
```

## Шифрование паролей

### Использование утилиты шифрования

```bash
# Перейдите в папку tools
cd "C:\WindowsLauncher\tools"

# Скомпилируйте и запустите утилиту
dotnet run "your_app_password_here"

# Или интерактивный режим
dotnet run
```

Утилита выдаст зашифрованный пароль для использования в базе данных.

### Ручное шифрование в коде

```csharp
var encryptionService = serviceProvider.GetService<IEncryptionService>();
var encryptedPassword = encryptionService.Encrypt("your_app_password");
```

## Использование email системы

### Отправка сообщения

1. В главном окне нажмите кнопку ✈️ (Compose Email)
2. Выберите получателей из адресной книги
3. Введите тему и текст сообщения
4. Прикрепите файлы при необходимости (до 25 МБ)
5. Нажмите "Отправить"

### Управление контактами

1. Нажмите кнопку 📇 (Address Book)
2. Добавляйте, редактируйте, удаляйте контакты
3. Импорт/экспорт через CSV файлы
4. Группировка по отделам

### Fallback система

Система автоматически:
- Пытается отправить через основной сервер (Primary)
- При ошибке переключается на резервный (Backup)
- Ведет счетчик ошибок для мониторинга
- Логирует все операции для диагностики

## Производственная настройка

### Безопасность

1. **Никогда не коммитьте незашифрованные пароли**
2. **Используйте корпоративные SMTP серверы**
3. **Настройте SSL/TLS соединения**
4. **Ограничьте права доступа к базе данных**

### Корпоративные SMTP серверы

**Microsoft Exchange:**
```
Host: your-exchange-server.company.com
Port: 587 или 25
Use STARTTLS: ✅
```

**Office 365:**
```
Host: smtp.office365.com
Port: 587
Use STARTTLS: ✅
```

**Google Workspace:**
```
Host: smtp.gmail.com
Port: 587
Use STARTTLS: ✅
```

### Мониторинг

Система предоставляет логи и метрики:
- Успешные/неудачные отправки
- Время отправки
- Использование основного/резервного сервера
- Счетчики ошибок по серверам

## Устранение неисправностей

### Частые ошибки

**"Authentication failed"**
- Проверьте корректность username/password
- Убедитесь что используются App Passwords, а не обычные пароли
- Проверьте что двухфакторная аутентификация включена

**"Connection timeout"**
- Проверьте настройки firewall
- Убедитесь в корректности хоста и порта
- Попробуйте другие порты (25, 465, 587)

**"Password decryption failed"**
- Перешифруйте пароль с помощью актуального EncryptionService
- Проверьте что ключ шифрования не изменился

### Диагностические команды

```sql
-- Проверка настроек SMTP
SELECT * FROM SMTP_SETTINGS WHERE IS_ACTIVE = 1;

-- Проверка контактов
SELECT COUNT(*) as TotalContacts FROM CONTACTS WHERE IS_ACTIVE = 1;

-- Проверка ошибок серверов
SELECT HOST, CONSECUTIVE_ERRORS, LAST_SUCCESSFUL_SEND 
FROM SMTP_SETTINGS 
ORDER BY CONSECUTIVE_ERRORS DESC;
```

### Логи

Проверьте логи приложения для детальной диагностики:
- Попытки подключения к SMTP
- Ошибки аутентификации
- Время отправки сообщений
- Переключения между серверами

## API Reference

### EmailService методы

```csharp
// Отправка сообщения с fallback
Task<EmailSendResult> SendEmailAsync(EmailMessage message)

// Простая отправка
Task<EmailSendResult> SendSimpleEmailAsync(string to, string subject, string body, bool isHtml = false)

// С вложением
Task<EmailSendResult> SendEmailWithAttachmentAsync(string to, string subject, string body, string attachmentPath)

// Тестирование соединения
Task<EmailTestResult> TestSmtpConnectionAsync(SmtpSettings settings)
```

### AddressBookService методы

```csharp
// Управление контактами
Task<IReadOnlyList<Contact>> GetAllContactsAsync()
Task<Contact> CreateContactAsync(Contact contact, string createdBy)
Task<Contact> UpdateContactAsync(Contact contact)
Task<bool> DeleteContactAsync(int contactId)

// Импорт/экспорт
Task<ImportResult> ImportContactsFromCsvAsync(string csvContent, string createdBy)
Task<string> ExportContactsToCsvAsync()
```

## Заключение

Email система WindowsLauncher предоставляет enterprise-ready функциональность для корпоративных сред с акцентом на надежность, безопасность и простоту использования. Fallback система обеспечивает непрерывность работы даже при сбоях основного SMTP сервера.