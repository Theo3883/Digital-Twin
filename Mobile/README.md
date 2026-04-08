# DigitalTwin Mobile Embedded .NET Engine

This is a separate, clean architecture implementation of the DigitalTwin mobile app backend that runs embedded within an iOS SwiftUI app. It follows proper clean architecture principles and calls the existing DigitalTwin WebAPI.

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                        SwiftUI App                          │
│  ┌─────────────────────────────────────────────────────────┐ │
│  │              MobileEngine (Facade)                     │ │
│  └─────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
                               │
                               │ JSON Strings
                               ▼
┌─────────────────────────────────────────────────────────────┐
│                  .NET Embedded Engine                       │
│                                                             │
│  ┌─────────────────────────────────────────────────────────┐ │
│  │                   Application Layer                     │ │
│  │  (Orchestration - AuthService, PatientService, etc.)   │ │
│  └─────────────────────────────────────────────────────────┘ │
│                               │                             │
│                               ▼                             │
│  ┌─────────────────────────────────────────────────────────┐ │
│  │                    Domain Layer                         │ │
│  │     (Business Logic - SyncService, Models, etc.)       │ │
│  └─────────────────────────────────────────────────────────┘ │
│                               │                             │
│                               ▼                             │
│  ┌─────────────────────────────────────────────────────────┐ │
│  │                Infrastructure Layer                     │ │
│  │  (SQLite Repos, HTTP Client to WebAPI, etc.)          │ │
│  └─────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
                               │
                               │ HTTP Calls
                               ▼
┌─────────────────────────────────────────────────────────────┐
│                  Existing WebAPI Server                     │
│             (Writes to Postgres Database)                   │
└─────────────────────────────────────────────────────────────┘
```

## Clean Architecture Principles

### Dependency Flow: Infrastructure ← Domain ← Application

1. **Domain Layer** (`DigitalTwin.Mobile.Domain`)
   - Contains business logic and domain models
   - Defines interfaces for external concerns
   - No dependencies on other layers
   - Pure business rules and entities

2. **Application Layer** (`DigitalTwin.Mobile.Application`) 
   - Orchestrates use cases and workflows
   - Depends only on Domain layer
   - Contains DTOs and application services
   - Coordinates between domain and infrastructure

3. **Infrastructure Layer** (`DigitalTwin.Mobile.Infrastructure`)
   - Implements domain interfaces
   - Contains SQLite repositories and HTTP clients
   - Depends on Domain layer
   - Handles external concerns (database, API calls)

4. **Engine Layer** (`DigitalTwin.Mobile.Engine`)
   - Entry point for SwiftUI
   - Provides simple facade over complex .NET functionality
   - Handles dependency injection setup
   - Returns JSON strings to SwiftUI

## Key Features

### Offline-First Architecture
- Local SQLite database for all data
- Tracks sync status for each entity
- Works completely offline
- Bidirectional sync with cloud when online

### Clean Separation of Concerns
- **SwiftUI**: UI presentation and native iOS features (HealthKit, etc.)
- **Mobile .NET Engine**: Business logic and data management
- **Existing WebAPI**: Cloud persistence and server-side logic

### Simple SwiftUI Integration
The `MobileEngine` class provides a simple facade:

```csharp
// Authentication
await engine.AuthenticateAsync(googleIdToken)
await engine.GetCurrentUserAsync()

// Patient Profile
await engine.GetPatientProfileAsync()
await engine.UpdatePatientProfileAsync(updateJson)

// Vital Signs
await engine.RecordVitalSignAsync(vitalJson)
await engine.GetVitalSignsAsync(fromDate, toDate)

// Synchronization
await engine.PerformSyncAsync()
await engine.PushLocalChangesAsync()
```

## Project Structure

```
Mobile/
├── DigitalTwin.Mobile.Domain/           # Business logic
│   ├── Models/                          # Domain entities
│   ├── Enums/                          # Domain enums
│   ├── Interfaces/                     # Repository interfaces
│   └── Services/                       # Domain services
├── DigitalTwin.Mobile.Application/      # Use case orchestration
│   ├── DTOs/                           # Data transfer objects
│   └── Services/                       # Application services
├── DigitalTwin.Mobile.Infrastructure/   # External concerns
│   ├── Data/                           # SQLite DbContext
│   ├── Repositories/                   # SQLite implementations
│   └── Services/                       # HTTP client to WebAPI
└── DigitalTwin.Mobile.Engine/           # SwiftUI facade
    ├── MobileEngine.cs                 # Main entry point
    └── ServiceCollectionExtensions.cs  # DI setup
```

## Usage from SwiftUI

1. **Initialize the engine:**
```swift
let databasePath = // ... path to SQLite file
let apiBaseUrl = "https://your-api.com"
let engine = MobileEngine(databasePath, apiBaseUrl)

// Initialize database on first run
await engine.initializeDatabaseAsync()
```

2. **Authenticate user:**
```swift
let googleIdToken = // ... from Google Sign-In
let result = await engine.authenticateAsync(googleIdToken)
// Parse JSON result
```

3. **Sync data:**
```swift
// Background sync
await engine.performSyncAsync()

// Record vital signs from HealthKit
let vitalsJson = // ... serialize HealthKit data
await engine.recordVitalSignsAsync(vitalsJson)
```

## Benefits of This Architecture

1. **Clean Separation**: Mobile app has completely separate codebase from server
2. **Proper Dependencies**: Infrastructure depends on Domain, not vice versa  
3. **Testable**: Each layer can be unit tested independently
4. **Maintainable**: Clear boundaries and responsibilities
5. **Offline-First**: Works without network connectivity
6. **Reusable**: Business logic can be shared across platforms
7. **Simple Integration**: SwiftUI only needs to call simple JSON methods

## Sync Strategy

The mobile app maintains its own local SQLite database and syncs with the cloud:

1. **Local Changes**: Tracked with `IsSynced` flags
2. **Push Sync**: Sends unsynced local data to WebAPI
3. **Pull Sync**: Fetches updates from WebAPI  
4. **Conflict Resolution**: Uses null-coalescing merge (cloud wins when present)
5. **Idempotency**: All sync operations use request IDs to prevent duplicates

This ensures the mobile app works offline and syncs seamlessly when online.