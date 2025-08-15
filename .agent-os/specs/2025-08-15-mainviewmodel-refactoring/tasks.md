# Spec Tasks

## Tasks

- [ ] 1. Setup MediatR Infrastructure
  - [ ] 1.1 Write tests for MediatR configuration and service registration
  - [ ] 1.2 Add MediatR NuGet package to WindowsLauncher.UI project
  - [ ] 1.3 Configure MediatR services in DI container with assembly scanning
  - [ ] 1.4 Create base notification and request types for ViewModel communication
  - [ ] 1.5 Verify all tests pass

- [ ] 2. Extract ApplicationManagementViewModel
  - [ ] 2.1 Write tests for ApplicationManagementViewModel functionality
  - [ ] 2.2 Create ApplicationManagementViewModel class with extracted properties and commands
  - [ ] 2.3 Implement application launching, filtering, and search logic
  - [ ] 2.4 Add MediatR notification handling for user changes and refresh requests
  - [ ] 2.5 Register ApplicationManagementViewModel in DI container
  - [ ] 2.6 Verify all tests pass

- [ ] 3. Extract CategoryManagementViewModel
  - [ ] 3.1 Write tests for CategoryManagementViewModel including checkbox logic
  - [ ] 3.2 Create CategoryManagementViewModel with category filtering and selection
  - [ ] 3.3 Implement complex checkbox state management (All category logic)
  - [ ] 3.4 Extract CategoryViewModel class to separate file
  - [ ] 3.5 Add MediatR notification for filter changes
  - [ ] 3.6 Verify all tests pass

- [ ] 4. Extract UserSessionViewModel
  - [ ] 4.1 Write tests for UserSessionViewModel authentication and session management
  - [ ] 4.2 Create UserSessionViewModel with user authentication and settings
  - [ ] 4.3 Implement logout, switch user, and settings management logic
  - [ ] 4.4 Add MediatR notifications for user changes and session events
  - [ ] 4.5 Handle guest user special cases and reinitialization logic
  - [ ] 4.6 Verify all tests pass

- [ ] 5. Extract AndroidStatusViewModel
  - [ ] 5.1 Write tests for AndroidStatusViewModel status management
  - [ ] 5.2 Create AndroidStatusViewModel with WSA status monitoring
  - [ ] 5.3 Implement status change event handling and display logic
  - [ ] 5.4 Add localization support for status texts and tooltips
  - [ ] 5.5 Handle lifecycle management and subscription cleanup
  - [ ] 5.6 Verify all tests pass

- [ ] 6. Refactor MainViewModel as Composition Root
  - [ ] 6.1 Write tests for MainViewModel orchestration and property delegation
  - [ ] 6.2 Modify MainViewModel to compose and orchestrate child ViewModels
  - [ ] 6.3 Implement property delegation pattern for XAML binding compatibility
  - [ ] 6.4 Add command forwarding to appropriate child ViewModels
  - [ ] 6.5 Implement MediatR coordination between child ViewModels
  - [ ] 6.6 Reduce MainViewModel to under 300 lines
  - [ ] 6.7 Verify all tests pass

- [ ] 7. Ensure Backward Compatibility
  - [ ] 7.1 Write integration tests for XAML binding compatibility
  - [ ] 7.2 Verify all existing XAML bindings continue to work
  - [ ] 7.3 Test command bindings and event handling
  - [ ] 7.4 Validate initialization and lifecycle behavior
  - [ ] 7.5 Performance test to ensure no regressions
  - [ ] 7.6 Verify all tests pass

- [ ] 8. Documentation and Cleanup
  - [ ] 8.1 Write tests for edge cases and error handling
  - [ ] 8.2 Add XML documentation to all new ViewModels and interfaces
  - [ ] 8.3 Update architecture documentation with new ViewModel structure
  - [ ] 8.4 Remove dead code and cleanup unused dependencies
  - [ ] 8.5 Final performance validation and memory leak testing
  - [ ] 8.6 Verify all tests pass and achieve 90%+ coverage target