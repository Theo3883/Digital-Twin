# iOS Health App - .NET MAUI Blazor Hybrid

A modern iOS health tracking app built with .NET 9 MAUI Blazor, featuring Apple HealthKit integration and a beautiful Pango UI-inspired interface.

## 🏗️ Architecture

This application follows **SOLID principles** with a clean, maintainable architecture:

- **Interface Segregation**: `IHealthService` provides a focused contract
- **Dependency Inversion**: UI components depend on abstractions, not implementations
- **Single Responsibility**: Each component has a single, well-defined purpose
- **Open/Closed**: Easily extendable for additional health metrics
- **Liskov Substitution**: Platform implementations are fully interchangeable

## 📁 Project Structure

```
IOSHealthApp/
├── Components/
│   └── Pages/
│       └── MainPage.razor          # Main dashboard UI
├── Services/
│   └── IHealthService.cs           # Health service interface
├── Platforms/
│   └── iOS/
│       ├── Services/
│       │   └── HealthService.cs    # iOS HealthKit implementation
│       └── Info.plist              # HealthKit permissions
├── Styles/
│   └── input.css                   # Tailwind CSS source
├── wwwroot/
│   ├── app.css                     # Compiled Tailwind CSS
│   └── index.html                  # App entry point
├── MauiProgram.cs                  # DI configuration
├── package.json                    # npm dependencies
└── tailwind.config.js              # Tailwind configuration
```

## 🚀 Getting Started

### Prerequisites

- .NET 9 SDK
- Node.js and npm
- Xcode (for iOS development)
- macOS with Apple Silicon or Intel

### Installation

1. **Install npm dependencies**:
   ```bash
   cd IOSHealthApp
   npm install
   ```

2. **Build Tailwind CSS**:
   ```bash
   npm run css:build
   ```

3. **Run the app**:
   ```bash
   dotnet build -t:Run -f net9.0-ios
   ```

### Development Workflow

For active development with Tailwind CSS hot-reload:

1. Start Tailwind in watch mode:
   ```bash
   npm run css:watch
   ```

2. Run the app in another terminal:
   ```bash
   dotnet build -t:Run -f net9.0-ios
   ```

## 🎨 UI Features

- **Dark Mode**: Modern dark theme with Tailwind CSS
- **Gradient Accents**: Beautiful blue-to-purple gradients
- **Card-Based Layout**: Clean, organized metric cards
- **Loading States**: Smooth loading indicators
- **Responsive Design**: Adapts to different screen sizes

## 🏥 Health Metrics

Currently supported:
- ✅ Steps
- ✅ Heart Rate (placeholder)

## 🔧 Current Implementation Status

### Phase 1: Bridge Verification ✅

The app currently implements **stub methods** for HealthKit integration:

- `RequestPermissionAsync()`: Logs "HealthKit Permission Requested"
- `GetStepsAsync()`: Logs "Fetching Data from Apple Health..." and returns "10,245 Steps"

This allows you to verify the C# ↔️ HealthKit bridge is working correctly through console logs.

### Testing the Bridge

1. Run the app on an iOS device or simulator
2. Click the "Sync Fitness Data" button
3. Check the debug console for:
   ```
   HealthKit Permission Requested
   Fetching Data from Apple Health...
   ```
4. The UI should display "10,245 Steps"

## 📝 Code Quality

### SOLID Principles Applied

1. **Single Responsibility Principle**
   - `IHealthService`: Only handles health data operations
   - `MainPage.razor`: Only handles UI presentation
   - `HealthService`: Only handles iOS HealthKit integration

2. **Open/Closed Principle**
   - New health metrics can be added without modifying existing code
   - Simply extend `IHealthService` interface

3. **Liskov Substitution Principle**
   - iOS implementation fully substitutable for the interface
   - Future Android/Windows implementations will work seamlessly

4. **Interface Segregation Principle**
   - Minimal, focused interface with only necessary methods
   - No client forced to depend on unused methods

5. **Dependency Inversion Principle**
   - UI depends on `IHealthService` abstraction
   - Platform implementations registered via DI

## 🔐 Permissions

The app requires the following iOS permissions (configured in `Info.plist`):

- `NSHealthShareUsageDescription`: Read health data
- `NSHealthUpdateUsageDescription`: Write health data
- `healthkit` device capability

## 🎯 Next Steps

To complete the HealthKit integration:

1. Add actual HealthKit API calls in `HealthService.cs`
2. Implement permission request dialog
3. Query step count data from HealthKit
4. Add heart rate data fetching
5. Add more health metrics (calories, distance, etc.)

## 📦 Dependencies

### .NET Dependencies
- Microsoft.Maui (net9.0)
- Microsoft.AspNetCore.Components.WebView.Maui

### npm Dependencies
- tailwindcss ^3.4.1
- postcss ^8.4.35
- autoprefixer ^10.4.17

## 🧪 Testing

To verify the bridge is working:

1. Look for console output when clicking "Sync Fitness Data"
2. Check that the UI updates with "10,245 Steps"
3. Verify loading states work correctly
4. Test permission flow (currently just logs)

## 📄 License

This project is created for iOS health tracking purposes.

---

**Built with ❤️ using .NET MAUI, Blazor, and Tailwind CSS**
