# DigitalTwin iOS App Deployment Guide

This guide covers the complete deployment process for the new SwiftUI-based DigitalTwin iOS app with embedded .NET engine.

## Prerequisites

### Development Environment
- **Xcode 16.0+** (for iOS 18.0+ deployment target)
- **.NET 10.0 SDK** with iOS workload
- **Apple Developer Account** (for App Store deployment)
- **macOS Sonoma 14.0+** (recommended)

### .NET iOS Workload Installation
```bash
# Install .NET iOS workload
dotnet workload install ios

# Verify installation
dotnet workload list
```

## Build Process

### 1. Build the .NET Mobile Engine

```bash
# Navigate to the Mobile directory
cd Mobile/

# Restore NuGet packages
dotnet restore

# Build for iOS ARM64 (device)
dotnet publish DigitalTwin.Mobile.Engine \
  -c Release \
  -r ios-arm64 \
  --self-contained \
  -p:PublishAot=true \
  -p:StripSymbols=true

# Build for iOS Simulator (x64)
dotnet publish DigitalTwin.Mobile.Engine \
  -c Release \
  -r iossimulator-x64 \
  --self-contained \
  -p:PublishAot=true \
  -p:StripSymbols=true

# Build for iOS Simulator (ARM64 - M1/M2 Macs)
dotnet publish DigitalTwin.Mobile.Engine \
  -c Release \
  -r iossimulator-arm64 \
  --self-contained \
  -p:PublishAot=true \
  -p:StripSymbols=true
```

### 2. Create Universal Framework

```bash
# Create universal framework for simulator architectures
lipo -create \
  Mobile/DigitalTwin.Mobile.Engine/bin/Release/net10.0/iossimulator-x64/publish/DigitalTwin.Mobile.Engine.dylib \
  Mobile/DigitalTwin.Mobile.Engine/bin/Release/net10.0/iossimulator-arm64/publish/DigitalTwin.Mobile.Engine.dylib \
  -output SwiftUIApp/Frameworks/DigitalTwin.Mobile.Engine.simulator.dylib

# Copy device framework
cp Mobile/DigitalTwin.Mobile.Engine/bin/Release/net10.0/ios-arm64/publish/DigitalTwin.Mobile.Engine.dylib \
   SwiftUIApp/Frameworks/DigitalTwin.Mobile.Engine.device.dylib
```

### 3. Configure Xcode Project

1. **Add Frameworks to Xcode:**
   - Open `SwiftUIApp/DigitalTwinApp.xcodeproj`
   - Add both `.dylib` files to the project
   - Set "Embed & Sign" for the frameworks

2. **Configure Build Settings:**
   - Set `IPHONEOS_DEPLOYMENT_TARGET` to `18.0`
   - Add framework search paths
   - Configure code signing

3. **Add Capabilities:**
   - HealthKit
   - Background App Refresh
   - Push Notifications (optional)

### 4. Build iOS App

```bash
# Build for simulator
xcodebuild -project SwiftUIApp/DigitalTwinApp.xcodeproj \
  -scheme DigitalTwinApp \
  -sdk iphonesimulator \
  -configuration Release \
  build

# Build for device
xcodebuild -project SwiftUIApp/DigitalTwinApp.xcodeproj \
  -scheme DigitalTwinApp \
  -sdk iphoneos \
  -configuration Release \
  build

# Archive for App Store
xcodebuild -project SwiftUIApp/DigitalTwinApp.xcodeproj \
  -scheme DigitalTwinApp \
  -sdk iphoneos \
  -configuration Release \
  archive \
  -archivePath build/DigitalTwinApp.xcarchive
```

## Configuration

### 1. API Configuration

Update the API base URL in `MobileEngineWrapper.swift`:

```swift
// Production API URL
self.apiBaseUrl = "https://api.digitaltwin.com"

// Staging API URL (for testing)
// self.apiBaseUrl = "https://staging-api.digitaltwin.com"
```

### 2. Google Sign-In Setup

1. **Configure Google Services:**
   - Download `GoogleService-Info.plist` from Firebase Console
   - Add to Xcode project
   - Configure URL schemes in Info.plist

2. **Add Google Sign-In SDK:**
   ```swift
   // Add to Package.swift or Xcode Package Manager
   .package(url: "https://github.com/google/GoogleSignIn-iOS", from: "7.0.0")
   ```

### 3. App Store Connect Configuration

1. **App Information:**
   - Bundle ID: `com.digitaltwin.mobile`
   - App Category: Medical
   - Content Rating: 4+ (Medical/Treatment Information)

2. **Privacy Information:**
   - Health data usage description
   - Face ID usage description
   - Background app refresh usage

3. **App Review Information:**
   - Provide test account credentials
   - Include demo health data for review
   - Explain medical use case clearly

## Testing

### 1. Unit Tests

```bash
# Run .NET tests
cd Mobile/
dotnet test

# Run Swift tests (if implemented)
cd SwiftUIApp/
xcodebuild test -project DigitalTwinApp.xcodeproj -scheme DigitalTwinApp -destination 'platform=iOS Simulator,name=iPhone 15 Pro'
```

### 2. Integration Tests

1. **HealthKit Integration:**
   - Test on physical device with Health app data
   - Verify read/write permissions
   - Test background sync

2. **Biometric Authentication:**
   - Test Face ID/Touch ID enrollment
   - Test authentication flows
   - Test fallback to passcode

3. **Background Sync:**
   - Test background app refresh
   - Verify sync scheduling
   - Test notification delivery

### 3. Performance Testing

1. **.NET Engine Performance:**
   - Measure SQLite query performance
   - Test sync operation timing
   - Monitor memory usage

2. **UI Performance:**
   - Test Liquid Glass rendering on older devices
   - Verify smooth scrolling in vital signs list
   - Test large dataset handling

## Deployment

### 1. TestFlight Distribution

```bash
# Upload to TestFlight
xcodebuild -exportArchive \
  -archivePath build/DigitalTwinApp.xcarchive \
  -exportPath build/export \
  -exportOptionsPlist ExportOptions.plist

# Upload using altool or Transporter app
xcrun altool --upload-app \
  --type ios \
  --file build/export/DigitalTwinApp.ipa \
  --username "your-apple-id@example.com" \
  --password "app-specific-password"
```

### 2. App Store Submission

1. **Pre-submission Checklist:**
   - [ ] App Store Review Guidelines compliance
   - [ ] Medical device regulations compliance (if applicable)
   - [ ] Privacy policy updated and accessible
   - [ ] Terms of service updated
   - [ ] Test account provided for review
   - [ ] App metadata and screenshots prepared

2. **Submission Process:**
   - Upload build via Xcode or Transporter
   - Complete App Store Connect metadata
   - Submit for review
   - Respond to reviewer feedback if needed

### 3. Production Monitoring

1. **Crash Reporting:**
   - Configure Xcode Organizer crash reports
   - Set up Firebase Crashlytics (optional)
   - Monitor .NET engine exceptions

2. **Analytics:**
   - Track app usage patterns
   - Monitor sync success rates
   - Track HealthKit integration usage

3. **Performance Monitoring:**
   - Monitor API response times
   - Track background sync efficiency
   - Monitor battery usage impact

## Migration from MAUI

### 1. Data Migration

The new SwiftUI app uses the same .NET backend and database schema, so existing user data will be preserved automatically.

### 2. User Communication

1. **In-app Migration Notice:**
   - Display migration announcement in current MAUI app
   - Provide timeline for transition
   - Highlight new features

2. **App Store Transition:**
   - Update app description to mention new version
   - Provide migration guide in app store notes
   - Ensure seamless data continuity

### 3. Deprecation Timeline

1. **Phase 1 (Week 1-2):** Release SwiftUI app as update
2. **Phase 2 (Week 3-4):** Monitor adoption and fix critical issues
3. **Phase 3 (Week 5-6):** Deprecate MAUI-specific features
4. **Phase 4 (Week 7+):** Full transition to SwiftUI codebase

## Troubleshooting

### Common Build Issues

1. **.NET AOT Compilation Errors:**
   ```bash
   # Enable verbose logging
   dotnet publish -v diagnostic
   
   # Check for unsupported APIs
   # Review trimming warnings
   ```

2. **Xcode Linking Errors:**
   - Verify framework paths in Build Settings
   - Check code signing configuration
   - Ensure all required capabilities are enabled

3. **Runtime Errors:**
   - Check device logs in Xcode Console
   - Verify .NET engine initialization
   - Test C bridge function calls

### Performance Issues

1. **Slow App Launch:**
   - Profile .NET engine initialization
   - Optimize database migration queries
   - Consider lazy loading for non-critical services

2. **Memory Issues:**
   - Monitor .NET heap usage
   - Check for SwiftUI view retention cycles
   - Profile HealthKit data queries

### App Store Review Issues

1. **HealthKit Rejection:**
   - Ensure clear usage descriptions
   - Provide medical use case justification
   - Test on devices without health data

2. **Background Processing Rejection:**
   - Justify background sync necessity
   - Ensure proper task completion
   - Limit background execution time

## Support and Maintenance

### 1. Update Process

1. **.NET Engine Updates:**
   - Update Mobile projects
   - Rebuild and test native libraries
   - Update Xcode project with new frameworks

2. **SwiftUI Updates:**
   - Update iOS deployment target as needed
   - Test new iOS features and APIs
   - Update Liquid Glass implementation for new iOS versions

### 2. Monitoring and Alerts

- Set up automated build monitoring
- Configure crash rate alerts
- Monitor API health and sync success rates
- Track user adoption of new features

The SwiftUI app is now ready for production deployment with full feature parity to the MAUI version, plus enhanced native iOS integration and modern UI styling.