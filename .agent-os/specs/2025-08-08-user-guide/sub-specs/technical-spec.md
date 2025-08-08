# Technical Specification

This is the technical specification for the spec detailed in @.agent-os/specs/2025-08-08-user-guide/spec.md

## Technical Requirements

- **HTML-based Help System**: Встроенная справочная система на базе WebView2 компонента с HTML/CSS/JS
- **Integration Point**: Добавление Help меню в MainWindow с командой OpenHelpCommand  
- **Content Structure**: Модульная структура HTML страниц для разных разделов справки
- **Asset Management**: Встроенные ресурсы (HTML, CSS, изображения) в проект как Embedded Resources
- **Localization Support**: Поддержка ru-RU и en-US через LocalizationHelper integration
- **Touch Optimization**: CSS стили оптимизированные для сенсорного управления (min button size 48px)
- **Responsive Design**: Адаптивная верстка для экранов 1024x768 и 1920x1080
- **Navigation System**: Боковое меню навигации с якорными ссылками и breadcrumb
- **Search Functionality**: JavaScript поиск по содержимому справки с подсветкой результатов
- **Print Support**: CSS медиа-запросы для корректной печати страниц справки
- **Screenshot Integration**: Автоматически обновляемые скриншоты интерфейса в разных языковых версиях
- **Context-Sensitive Help**: Возможность открытия справки на определенной странице по контексту
- **Keyboard Navigation**: Полная поддержка клавиатурной навигации для доступности

## External Dependencies

- **WebView2**: Microsoft.Web.WebView2 v1.0+ для отображения HTML контента
- **Justification**: Уже используется в проекте для WebView2ApplicationWindow, обеспечивает современный браузерный движок для HTML справки