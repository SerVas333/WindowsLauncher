# Spec Requirements Document

> Spec: MainViewModel Refactoring
> Created: 2025-08-15
> Status: Planning

## Overview

Refactor the monolithic MainViewModel.cs (1717+ lines) into a maintainable, testable, and SOLID-compliant architecture by separating concerns, extracting specialized ViewModels, and implementing proper dependency management.

## User Stories

### Developer Experience Improvement

As a **WPF Developer**, I want to work with focused, single-responsibility ViewModels, so that I can understand, modify, and test individual features without navigating through a massive file.

**Detailed Workflow:** Developer needs to modify the application launcher logic without affecting user authentication, category management, or Android subsystem status. They should be able to find the relevant ViewModel quickly, understand its dependencies, and make changes with confidence that other features won't break.

### Maintainability Enhancement 

As a **Software Architect**, I want the MainViewModel to follow MVVM best practices and SOLID principles, so that the codebase remains maintainable as new features are added.

**Detailed Workflow:** When adding new functionality like notification systems or advanced filtering, the architect should be able to identify the appropriate place to add code, create new focused ViewModels that integrate cleanly, and ensure proper separation of concerns without refactoring existing code.

### Testing Improvement

As a **QA Engineer**, I want individual ViewModels to be unit testable in isolation, so that I can write comprehensive tests for each feature area without complex mocking setups.

**Detailed Workflow:** QA needs to test category filtering logic independently from user authentication or application launching. They should be able to create focused unit tests with minimal setup and clear assertions about specific functionality.

## Spec Scope

1. **Extract Application Management ViewModel** - Separate application launching, filtering, and lifecycle management
2. **Extract Category Management ViewModel** - Isolate category filtering, selection, and checkbox logic  
3. **Extract User Session ViewModel** - Handle user authentication, settings, and session management
4. **Extract Android Status ViewModel** - Manage WSA status display and updates
5. **Implement Mediator Pattern** - Enable communication between extracted ViewModels
6. **Create Composition Root** - Main ViewModel becomes orchestrator of child ViewModels
7. **Improve Dependency Injection** - Proper scoping and service resolution
8. **Add Unit Tests** - Test coverage for each extracted ViewModel

## Out of Scope

- UI/XAML changes (ViewModels maintain same public interface)
- Business logic modifications in services layer
- Database schema changes
- Localization system modifications
- Material Design styling updates

## Expected Deliverable

1. **MainViewModel.cs reduced to <300 lines** - Acts as composition root and orchestrator
2. **4+ specialized ViewModels** - Each handling single responsibility area
3. **Mediator implementation** - Clean communication between ViewModels
4. **90%+ unit test coverage** - For all extracted ViewModels
5. **No breaking changes to UI** - Existing XAML bindings continue to work
6. **Improved performance** - Reduced memory usage and faster initialization