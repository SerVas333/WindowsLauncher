# Development Standards

> Override Priority: Highest
> 
> **Эти стандарты обязательны для всей команды разработки WindowsLauncher**

## Platform & Environment Standards

### Windows-Only Build Policy

**Критическое требование:** Весь WPF-проект должен собираться и запускаться исключительно из Windows-среды, несмотря на возможную разработку (редактирование кода, git-операции) из WSL.

- ✅ **Разрешено в WSL:** Git операции, редактирование кода, подготовка Pull Request
- ❌ **Запрещено в WSL:** Сборка C#/WPF, операции с XAML, запуск приложения
- 🔧 **Только Windows:** dotnet build, Visual Studio, XAML Designer, отладка

### Source Code Storage

**Общее хранилище исходников:** Хранить исходный код в общем каталоге, доступном как из Windows, так и из WSL:
```
/mnt/c/WindowsLauncher/   # WSL путь
C:\WindowsLauncher\       # Windows путь
```

**Правила синхронизации:**
- ✅ Исходный код (.cs, .xaml, .json) — синхронизируется
- ❌ Артефакты сборки (bin/, obj/, Generated/) — НЕ синхронизируются из WSL в Windows
- 🔄 Пересборка всех артефактов только в Windows после переключения

### Environment Separation

**Разделение задач среды:**
- **WSL:** Frontend инструментарий (npm, linter, генераторы кода), Git операции
- **Windows:** C#/WPF билды, операции с XAML, Visual Studio, отладка, тестирование

## Architecture Standards

### MVVM Pattern (Mandatory)

**Обязательный паттерн:** MVVM должен строго соблюдаться во всей архитектуре проекта.

```csharp
// ✅ Правильно: ViewModel независима от View
public class MainViewModel : ViewModelBase
{
    private readonly IApplicationService _appService;
    
    public MainViewModel(IApplicationService appService)
    {
        _appService = appService;
    }
}

// ❌ Неправильно: прямое обращение к UI элементам
public class BadViewModel
{
    public void DoSomething()
    {
        var window = Application.Current.MainWindow; // ЗАПРЕЩЕНО
    }
}
```

**Требования:**
- Все ViewModel, сервисы, DI-контейнер строго отделены от View (XAML)
- ViewModels наследуются от ViewModelBase с INotifyPropertyChanged
- Команды через ICommand (RelayCommand, AsyncRelayCommand)
- Никаких прямых ссылок на UI элементы в бизнес-логике

### Dependency Injection Standards

**Инъекция зависимостей:** Только через конструкторы или DI-контейнер (Microsoft.Extensions.DependencyInjection).

```csharp
// ✅ Правильно: Constructor Injection
public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<UserService> _logger;
    
    public UserService(IUserRepository userRepository, ILogger<UserService> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
    }
}

// ❌ Неправильно: Service Locator pattern
public class BadService
{
    public void DoWork()
    {
        var service = ServiceProvider.GetService<ISomeService>(); // ЗАПРЕЩЕНО
    }
}
```

### Layer Architecture

**Обязательное разбиение на слои:**
```
WindowsLauncher.UI/          # Презентационный слой (XAML, ViewModels)
WindowsLauncher.Services/    # Бизнес-логика и сервисы
WindowsLauncher.Data/        # Доступ к данным (EF Core, репозитории)
WindowsLauncher.Core/        # Модели, интерфейсы, перечисления
WindowsLauncher.Tests/       # Юнит-тесты
```

## XAML & UI Standards

### Resource Organization

**Стандартизация ресурсов:** ResourceDictionary только в определённых папках с централизованным управлением стилями.

```
WindowsLauncher.UI/Styles/
├── MaterialColors.xaml      # Корпоративная цветовая палитра KDV
├── MaterialStyles.xaml      # Material Design базовые стили  
└── CorporateStyles.xaml     # Кастомные корпоративные элементы
```

**Порядок подключения в App.xaml:** Цвета → Material → Корпоративные стили

### XAML Code Style

**Обязательное использование XamlStyler** для форматирования XAML:
- Единообразное форматирование атрибутов
- Консистентный порядок элементов
- Автоматическое форматирование при сохранении

**Централизованные стили:**
```xml
<!-- ✅ Правильно: использование централизованных стилей -->
<Button Style="{StaticResource CorporateButton}" Content="OK"/>

<!-- ❌ Неправильно: inline стили -->
<Button Background="Red" Foreground="White" Content="OK"/>
```

## Localization Standards

### Resource Files Only

**Локализация:** Использование только .resx-файлов для любых строк внутри UI.

```csharp
// ✅ Правильно: использование ресурсов
Text = Properties.Resources.ButtonOK;

// ❌ Неправильно: хардкод строк
Text = "OK"; // ЗАПРЕЩЕНО
```

**Структура ресурсов:**
```
Properties/Resources/
├── Resources.resx           # Основной ресурс (en-US)
├── Resources.ru-RU.resx     # Русская локализация
└── Resources.Designer.cs    # Автогенерируемый код
```

## Testing Standards

### Unit Testing Requirements

**Обязательное покрытие тестами:**
- ✅ ViewModel логика и команды
- ✅ Бизнес-сервисы и их методы  
- ✅ Репозитории и доступ к данным
- ❌ UI код (XAML) не тестируется юнит-тестами

**Testing Framework:**
```csharp
// Обязательный стек для тестов
[Test] // или [Fact] для xUnit
public async Task UserService_CreateUser_ShouldReturnSuccess()
{
    // Arrange
    var mockRepo = new Mock<IUserRepository>();
    var service = new UserService(mockRepo.Object);
    
    // Act
    var result = await service.CreateUserAsync("testuser");
    
    // Assert
    Assert.IsTrue(result.IsSuccess);
}
```

**Выполнение тестов:** Тесты должны выполняться на локальной Windows-машине через Visual Studio Test Explorer или dotnet test.

## Build & Publishing Standards

### Build Configuration

**"Build Once, Run Anywhere" принцип:** Чёткие требования к dotnet SDK/Visual Studio для сборки под Windows.

**Требования к среде сборки:**
- Visual Studio 2022 или новее
- .NET 8.0 SDK
- Windows 10/11 (build machine)
- Все сборочные скрипты запускаются под Windows

### Dependency Management

**NuGet Dependencies:** Утверждённый список зависимостей для обеспечения совместимости с Windows платформой:

```xml
<!-- Основной стек (обязательные) -->
<PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="9.0.6" />
<PackageReference Include="MaterialDesignThemes" Version="5.2.1" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="9.0.6" />
<PackageReference Include="FirebirdSql.EntityFrameworkCore.Firebird" Version="11.1.2" />

<!-- UI и веб-интеграция -->
<PackageReference Include="Microsoft.Web.WebView2" Version="1.0.3351.48" />
<PackageReference Include="System.DirectoryServices" Version="9.0.6" />
```

**Проверка совместимости:** Все новые зависимости должны быть протестированы на совместимость с Windows-only сборками.

## Git Workflow Standards

### Cross-Environment Git Operations

**Автоматизация git-операций:** Максимальная автоматизация git операций и подготовки Pull Request через WSL-скрипты.

**Рекомендуемый workflow:**
```bash
# WSL: Git операции разрешены
git checkout -b feature/new-functionality
git add .
git commit -m "Add new functionality"
git push origin feature/new-functionality

# Windows: Сборка и тестирование
dotnet build --configuration Release
dotnet test
```

### Artifacts Management

**Критическое правило:** Никакие сборочные артефакты (bin/obj, Generated) не должны синхронизироваться из WSL в Windows.

**.gitignore обязательные записи:**
```gitignore
# Build artifacts - НЕ СИНХРОНИЗИРОВАТЬ
bin/
obj/
Generated/
*.user
*.suo
*.cache

# Разрешено синхронизировать
*.cs
*.xaml
*.json
*.md
```

## Quality Assurance

### Code Review Standards

**Обязательная проверка:**
- ✅ Соблюдение MVVM паттерна
- ✅ Использование DI через конструкторы
- ✅ Локализация через .resx файлы
- ✅ Централизованные XAML стили
- ✅ Покрытие тестами бизнес-логики
- ✅ Windows-only сборка успешна

### Performance Standards

**Требования к производительности:**
- Время запуска приложения < 5 секунд
- Отклик UI < 2 секунды
- Потребление RAM < 200MB в idle состоянии
- Поддержка работы 24/7 без перезагрузки