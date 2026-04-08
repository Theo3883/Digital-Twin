# DigitalTwin SwiftUI App

This is the SwiftUI frontend for the DigitalTwin mobile app that integrates with the embedded .NET Mobile Engine.

## Architecture

```
┌─────────────────────────────────────────┐
│              SwiftUI App                │
│                                         │
│  ┌─────────────────────────────────────┐ │
│  │        ContentView                  │ │
│  │  (Authentication, Dashboard, etc.)  │ │
│  └─────────────────────────────────────┘ │
│                    │                    │
│                    ▼                    │
│  ┌─────────────────────────────────────┐ │
│  │      MobileEngineWrapper            │ │
│  │    (Swift ObservableObject)         │ │
│  └─────────────────────────────────────┘ │
│                    │                    │
│                    ▼                    │
│  ┌─────────────────────────────────────┐ │
│  │        DotNetBridge                 │ │
│  │      (C Interop Layer)              │ │
│  └─────────────────────────────────────┘ │
└─────────────────────────────────────────┘
                     │
                     ▼ C ABI
┌─────────────────────────────────────────┐
│         .NET Mobile Engine              │
│       (Embedded Backend)                │
└─────────────────────────────────────────┘
```

## Key Components

### 1. ContentView
- Main SwiftUI interface
- Implements Apple's Liquid Glass styling (iOS 26+) with fallbacks
- Handles authentication, dashboard, vital signs, profile, and settings

### 2. MobileEngineWrapper
- `@ObservableObject` that provides SwiftUI-friendly interface
- Manages engine lifecycle and state
- Handles async operations and error states
- Publishes UI state changes

### 3. DotNetBridge
- Low-level C interop with the .NET engine
- Handles memory management and marshalling
- Converts between Swift types and JSON strings
- Provides error handling and type safety

### 4. NativeBridge (C# side)
- Exports C-compatible functions from .NET
- Handles async-to-sync conversion
- Manages JSON serialization/deserialization
- Provides memory management for Swift

## Liquid Glass Implementation

The app implements Apple's Liquid Glass APIs (iOS 26+) with fallbacks:

### Navigation Layer Styling
- **iOS 26+**: Uses `glassEffect()`, `GlassEffectContainer`, and `.buttonStyle(.glass)`
- **Older iOS**: Falls back to `Material` (`.ultraThinMaterial`, `.thinMaterial`)

### Usage Patterns
- Primary interactive controls: `.glassEffect(.regular.tint(...).interactive())`
- Navigation elements: Custom glass backgrounds with proper blending
- Cards and containers: `.glassEffect(.thin.tint(.primary.opacity(0.05)))`
- Buttons: `.buttonStyle(.glassProminent)` with glass effects

### Performance Considerations
- Minimizes active `GlassEffectContainer` count
- Uses glass effects primarily for navigation layer, not dense content
- Follows Apple's performance guidance for glass rendering

## Features Implemented

### ✅ Phase 4 Complete
- [x] SwiftUI app project structure
- [x] .NET engine integration via C bridge
- [x] Authentication flow (Google Sign-In ready)
- [x] Dashboard with vital signs overview
- [x] Patient profile management
- [x] Liquid Glass styling with iOS version gating
- [x] Error handling and loading states
- [x] Memory management for C interop

### 🚧 Ready for Implementation
- [ ] HealthKit integration (Phase 6)
- [ ] Background sync (Phase 6)
- [ ] Detailed vital signs views (Phase 5)
- [ ] Profile editing forms (Phase 5)
- [ ] Settings and preferences (Phase 5)

## Integration Points

### Authentication
```swift
// Google Sign-In integration point
let success = await engineWrapper.authenticate(googleIdToken: googleIdToken)
```

### Vital Signs Recording
```swift
// HealthKit integration point
let vitalSign = VitalSignInput(
    type: .heartRate,
    value: 72.0,
    unit: "bpm",
    source: "HealthKit",
    timestamp: Date()
)
await engineWrapper.recordVitalSign(vitalSign)
```

### Background Sync
```swift
// Background task integration point
let success = await engineWrapper.performSync()
```

## Build Requirements

### .NET Side
1. Build the Mobile Engine with NativeAOT:
```bash
cd Mobile/
dotnet publish DigitalTwin.Mobile.Engine -c Release -r ios-arm64 --self-contained
```

2. Copy the native library to the iOS project:
```bash
cp Mobile/DigitalTwin.Mobile.Engine/bin/Release/net10.0/ios-arm64/publish/DigitalTwin.Mobile.Engine.dylib SwiftUIApp/
```

### iOS Side
1. Add the .NET library to the Xcode project
2. Configure library search paths
3. Add HealthKit capabilities
4. Configure Google Sign-In SDK

## Next Steps (Phase 5)

1. **Implement detailed views**: Expand vital signs, profile, and settings views
2. **Add form validation**: Patient profile editing with proper validation
3. **Enhance dashboard**: More detailed charts and trends
4. **Improve error handling**: Better user feedback for sync errors
5. **Add offline indicators**: Show sync status and offline capabilities

## Next Steps (Phase 6)

1. **HealthKit integration**: Replace mock vital signs with real HealthKit data
2. **Background sync**: Implement BGTaskScheduler for background operations
3. **Native document scanning**: Replace C# OCR with native Vision framework
4. **Biometric authentication**: Add Face ID/Touch ID for app security

The SwiftUI app is now ready to be connected to the actual .NET Mobile Engine and can serve as the foundation for the complete iOS app migration.