# Technical Specification

This is the technical specification for the spec detailed in @.agent-os/specs/2025-08-15-mainviewmodel-refactoring/spec.md

## Technical Requirements

### Architecture Pattern Implementation
- **Mediator Pattern**: Implement IMediator interface using MediatR library for ViewModel communication
- **Composition Root**: MainViewModel becomes orchestrator, not implementer of business logic
- **Single Responsibility Principle**: Each extracted ViewModel handles one domain area
- **Dependency Injection**: Proper scoping for ViewModels (Scoped for user sessions, Transient for UI components)

### Extracted ViewModels Structure

#### ApplicationManagementViewModel
- **Responsibilities**: Application launching, filtering, collection management, search functionality
- **Properties**: `Applications`, `FilteredApplications`, `SearchText`, `ApplicationCount`, `HasNoApplications`, `IsLoading`
- **Commands**: `LaunchApplicationCommand`, `RefreshCommand`
- **Dependencies**: `IApplicationService`, `IAuthorizationService`, `IApplicationLifecycleService`

#### CategoryManagementViewModel  
- **Responsibilities**: Category filtering, selection logic, checkbox state management
- **Properties**: `LocalizedCategories`, `SelectedCategory`, `HasActiveFilter`
- **Commands**: `SelectCategoryCommand`, `ToggleSidebarCommand`
- **Dependencies**: `ICategoryManagementService`, `ILocalizationHelper`

#### UserSessionViewModel
- **Responsibilities**: User authentication, settings management, session lifecycle
- **Properties**: `CurrentUser`, `UserSettings`, `WindowTitle`, `LocalizedRole`, `CanManageSettings`
- **Commands**: `LogoutCommand`, `SwitchUserCommand`, `OpenSettingsCommand`, `OpenAdminCommand`
- **Dependencies**: `IAuthenticationService`, `ISessionManagementService`, `IAuthorizationService`

#### AndroidStatusViewModel
- **Responsibilities**: WSA status monitoring and display
- **Properties**: `ShowWSAStatus`, `WSAStatusText`, `WSAStatusTooltip`, `WSAStatusColor`
- **Dependencies**: `IAndroidSubsystemService`

### Собственная реализация Mediator
- **Событийная модель**: Использование стандартных .NET событий EventHandler<T>
- **Типы событий**: `ApplicationLaunched`, `UserChanged`, `CategoryFilterChanged`, `ApplicationsRefreshed`
- **Регистрация обработчиков**: Через DI контейнер и WeakReference для предотвращения утечек памяти

### Паттерны коммуникации
```csharp
// Пример: При смене пользователя уведомляем управление приложениями о перезагрузке
public class UserChangedEventArgs : EventArgs
{
    public User NewUser { get; }
    public UserChangedEventArgs(User newUser) => NewUser = newUser;
}

// Пример: При обновлении приложений уведомляем управление категориями
public class ApplicationsRefreshedEventArgs : EventArgs  
{
    public int Count { get; }
    public ApplicationsRefreshedEventArgs(int count) => Count = count;
}
```

### Performance Optimizations
- **Lazy Loading**: ViewModels initialize only when accessed
- **Async Operations**: All initialization and data loading operations remain async
- **Memory Management**: Proper disposal patterns for event subscriptions
- **UI Thread Safety**: Maintain current Dispatcher.InvokeAsync patterns

### Backward Compatibility
- **Property Delegation**: MainViewModel exposes child ViewModel properties for XAML binding
- **Command Forwarding**: MainViewModel forwards commands to appropriate child ViewModels
- **Event Bubbling**: Child ViewModel events bubble up through MainViewModel

## External Dependencies

**Нет внешних зависимостей** - Реализуем собственный паттерн Mediator на основе встроенных .NET событий и интерфейсов

### Integration Requirements
- Создание собственного интерфейса IViewModelMediator с MIT лицензией
- Использование стандартных .NET событий (EventHandler, EventArgs) для связи между ViewModels
- Регистрация в существующем DI контейнере Microsoft.Extensions.DependencyInjection