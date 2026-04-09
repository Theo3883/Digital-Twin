import SwiftUI

struct ContentView: View {
    @EnvironmentObject var engineWrapper: MobileEngineWrapper
    @State private var selectedTab = 0
    
    var body: some View {
        Group {
            if !engineWrapper.isInitialized {
                LoadingView(message: "Initializing DigitalTwin...")
            } else if !engineWrapper.isAuthenticated {
                AuthenticationView()
            } else {
                MainTabView(selectedTab: $selectedTab)
            }
        }
        .alert("Error", isPresented: .constant(engineWrapper.errorMessage != nil)) {
            Button("OK") {
                engineWrapper.errorMessage = nil
            }
        } message: {
            Text(engineWrapper.errorMessage ?? "")
        }
    }
}

// MARK: - Loading View

struct LoadingView: View {
    let message: String
    
    var body: some View {
        VStack(spacing: 20) {
            ProgressView()
                .scaleEffect(1.5)
            
            Text(message)
                .font(.headline)
                .foregroundColor(.secondary)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .background(Color(.systemBackground))
    }
}

// MARK: - Authentication View

struct AuthenticationView: View {
    @EnvironmentObject var engineWrapper: MobileEngineWrapper
    @StateObject private var googleSignIn = GoogleSignInService()
    @State private var isAuthenticating = false
    
    var body: some View {
        VStack(spacing: 40) {
            Spacer()
            
            // App Logo/Title
            VStack(spacing: 16) {
                Image(systemName: "heart.text.square.fill")
                    .font(.system(size: 80))
                    .foregroundColor(.blue)
                
                Text("DigitalTwin")
                    .font(.largeTitle)
                    .fontWeight(.bold)
                
                Text("Your Personal Health Companion")
                    .font(.headline)
                    .foregroundColor(.secondary)
            }
            
            Spacer()
            
            // Sign In Button with Liquid Glass (iOS 26+) or fallback
            VStack(spacing: 16) {
                Button(action: signInWithGoogle) {
                    HStack {
                        Image(systemName: "globe")
                        Text("Sign in with Google")
                            .fontWeight(.semibold)
                    }
                    .frame(maxWidth: .infinity)
                    .frame(height: 50)
                }
                .liquidGlassButtonStyle()
                .disabled(isAuthenticating)
                
                if isAuthenticating {
                    ProgressView("Signing in...")
                        .frame(maxWidth: .infinity)
                }
            }
            
            Spacer()
            
            // Privacy Notice
            Text("By signing in, you agree to our privacy policy and terms of service.")
                .font(.caption)
                .foregroundColor(.secondary)
                .multilineTextAlignment(.center)
                .padding(.horizontal)
        }
        .padding()
        .background(Color(.systemBackground))
    }
    
    private func signInWithGoogle() {
        isAuthenticating = true
        
        Task {
            do {
                let idToken = try await googleSignIn.signIn()
                let success = await engineWrapper.authenticate(googleIdToken: idToken)
                
                isAuthenticating = false
                if success {
                    print("Authentication successful")
                } else {
                    print("Authentication failed — backend rejected the token")
                }
            } catch {
                isAuthenticating = false
                print("Google Sign-In failed: \(error.localizedDescription)")
                engineWrapper.errorMessage = error.localizedDescription
            }
        }
    }
}

// MARK: - Main Tab View

struct MainTabView: View {
    @Binding var selectedTab: Int
    @EnvironmentObject var engineWrapper: MobileEngineWrapper
    
    var body: some View {
        TabView(selection: $selectedTab) {
            DashboardView()
                .tabItem {
                    Image(systemName: "house.fill")
                    Text("Dashboard")
                }
                .tag(0)
            
            VitalSignsView()
                .tabItem {
                    Image(systemName: "heart.fill")
                    Text("Vitals")
                }
                .tag(1)
            
            MedicationsView()
                .tabItem {
                    Image(systemName: "pills.fill")
                    Text("Meds")
                }
                .tag(2)
            
            MedicalAssistantView()
                .tabItem {
                    Image(systemName: "bubble.left.and.bubble.right.fill")
                    Text("Assistant")
                }
                .tag(3)
            
            EcgMonitorView()
                .tabItem {
                    Image(systemName: "waveform.path.ecg")
                    Text("ECG")
                }
                .tag(4)
            
            EnvironmentView()
                .tabItem {
                    Image(systemName: "cloud.sun.fill")
                    Text("Air")
                }
                .tag(5)
            
            ProfileView()
                .tabItem {
                    Image(systemName: "person.fill")
                    Text("Profile")
                }
                .tag(6)
            
            SettingsView()
                .tabItem {
                    Image(systemName: "gear")
                    Text("Settings")
                }
                .tag(7)
        }
        .liquidGlassTabViewStyle()
    }
}

// MARK: - Dashboard View

struct DashboardView: View {
    @EnvironmentObject var engineWrapper: MobileEngineWrapper
    @State private var recentVitals: [VitalSignInfo] = []
    
    var body: some View {
        NavigationView {
            ScrollView {
                VStack(spacing: 20) {
                    // Welcome Section
                    if let user = engineWrapper.currentUser {
                        WelcomeCard(user: user)
                    }
                    
                    // Coaching Advice Widget
                    if let advice = engineWrapper.coachingAdvice {
                        CoachingWidget(advice: advice)
                    }
                    
                    // Environment AQI Widget
                    if let env = engineWrapper.latestEnvironmentReading {
                        EnvironmentWidget(reading: env)
                    }
                    
                    // Sleep Summary Widget
                    if let latestSleep = engineWrapper.sleepSessions.first {
                        SleepWidget(session: latestSleep)
                    }
                    
                    // Active Medications Count
                    if !engineWrapper.medications.isEmpty {
                        MedicationsWidget(medications: engineWrapper.medications)
                    }
                    
                    // Quick Stats
                    QuickStatsSection(vitals: recentVitals)
                    
                    // Recent Activity
                    RecentActivitySection(vitals: recentVitals)
                    
                    Spacer(minLength: 100)
                }
                .padding()
            }
            .navigationTitle("Dashboard")
            .liquidGlassNavigationStyle()
            .task {
                await loadDashboardData()
            }
            .refreshable {
                await loadDashboardData()
            }
        }
    }
    
    private func loadDashboardData() async {
        let fromDate = Calendar.current.date(byAdding: .day, value: -7, to: Date())
        recentVitals = await engineWrapper.getVitalSigns(from: fromDate, to: Date())
        await engineWrapper.fetchCoachingAdvice()
        await engineWrapper.loadLatestEnvironmentReading()
        await engineWrapper.loadSleepSessions(from: fromDate, to: Date())
        await engineWrapper.loadMedications()
    }
}

// MARK: - Dashboard Widgets

struct CoachingWidget: View {
    let advice: CoachingAdviceInfo
    
    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            HStack {
                Image(systemName: "lightbulb.fill")
                    .foregroundColor(LiquidGlass.tealPrimary)
                Text("Today's Coaching")
                    .font(.headline)
            }
            Text(advice.advice)
                .font(.subheadline)
                .foregroundColor(.secondary)
                .lineLimit(3)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .glassCard(tint: LiquidGlass.tealPrimary.opacity(0.2))
    }
}

struct EnvironmentWidget: View {
    let reading: EnvironmentReadingInfo
    
    private var tintColor: Color {
        switch reading.airQualityLevel {
        case 0: return LiquidGlass.greenPositive
        case 1: return .yellow
        case 2: return .orange
        case 3: return LiquidGlass.redCritical
        default: return .gray
        }
    }
    
    var body: some View {
        HStack {
            VStack(alignment: .leading, spacing: 4) {
                Text("Air Quality")
                    .font(.caption).foregroundColor(.secondary)
                Text("\(reading.airQualityEmoji) \(reading.airQualityDisplay)")
                    .font(.headline)
            }
            Spacer()
            if let aqi = reading.aqiIndex {
                Text("AQI \(aqi)")
                    .font(.title2).fontWeight(.bold)
            }
            if let temp = reading.temperature {
                Text(String(format: "%.0f°C", temp))
                    .font(.title3)
                    .foregroundColor(.secondary)
            }
        }
        .glassCard(tint: tintColor.opacity(0.2))
    }
}

struct SleepWidget: View {
    let session: SleepSessionInfo
    
    var body: some View {
        HStack {
            Image(systemName: "moon.fill")
                .foregroundColor(LiquidGlass.purpleSleep)
            VStack(alignment: .leading, spacing: 2) {
                Text("Last Night")
                    .font(.caption).foregroundColor(.secondary)
                Text(session.durationFormatted)
                    .font(.headline)
            }
            Spacer()
            Text(session.qualityDisplay)
                .glassChip(tint: LiquidGlass.purpleSleep)
        }
        .glassCard(tint: LiquidGlass.purpleSleep.opacity(0.15))
    }
}

struct MedicationsWidget: View {
    let medications: [MedicationInfo]
    
    private var activeCount: Int {
        medications.filter(\.isActive).count
    }
    
    var body: some View {
        HStack {
            Image(systemName: "pills.fill")
                .foregroundColor(LiquidGlass.bluePrimary)
            VStack(alignment: .leading, spacing: 2) {
                Text("Active Medications")
                    .font(.caption).foregroundColor(.secondary)
                Text("\(activeCount) medication\(activeCount == 1 ? "" : "s")")
                    .font(.headline)
            }
            Spacer()
            Image(systemName: "chevron.right")
                .foregroundColor(.secondary)
        }
        .glassCard(tint: LiquidGlass.bluePrimary.opacity(0.15))
    }
}

// MARK: - Welcome Card

struct WelcomeCard: View {
    let user: UserInfo
    
    var body: some View {
        HStack {
            VStack(alignment: .leading, spacing: 4) {
                Text("Welcome back,")
                    .font(.subheadline)
                    .foregroundColor(.secondary)
                
                Text(user.displayName)
                    .font(.title2)
                    .fontWeight(.semibold)
            }
            
            Spacer()
            
            AsyncImage(url: user.photoUrl.flatMap(URL.init)) { image in
                image
                    .resizable()
                    .aspectRatio(contentMode: .fill)
            } placeholder: {
                Image(systemName: "person.circle.fill")
                    .font(.system(size: 40))
                    .foregroundColor(.blue)
            }
            .frame(width: 50, height: 50)
            .clipShape(Circle())
        }
        .padding()
        .liquidGlassCardStyle()
    }
}

// MARK: - Quick Stats Section

struct QuickStatsSection: View {
    let vitals: [VitalSignInfo]
    
    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            Text("Today's Overview")
                .font(.headline)
                .padding(.horizontal)
            
            LazyVGrid(columns: Array(repeating: GridItem(.flexible()), count: 2), spacing: 12) {
                ForEach(VitalSignType.allCases.prefix(4), id: \.self) { type in
                    QuickStatCard(
                        type: type,
                        value: latestValue(for: type),
                        trend: .stable // TODO: Calculate trend
                    )
                }
            }
            .padding(.horizontal)
        }
    }
    
    private func latestValue(for type: VitalSignType) -> Double? {
        vitals.filter { $0.type == type }.first?.value
    }
}

// MARK: - Quick Stat Card

struct QuickStatCard: View {
    let type: VitalSignType
    let value: Double?
    let trend: TrendDirection
    
    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            HStack {
                Text(type.displayName)
                    .font(.caption)
                    .foregroundColor(.secondary)
                
                Spacer()
                
                Image(systemName: trend.iconName)
                    .font(.caption)
                    .foregroundColor(trend.color)
            }
            
            if let value = value {
                Text("\(value, specifier: "%.1f")")
                    .font(.title2)
                    .fontWeight(.semibold)
                
                Text(type.unit)
                    .font(.caption)
                    .foregroundColor(.secondary)
            } else {
                Text("No data")
                    .font(.subheadline)
                    .foregroundColor(.secondary)
            }
        }
        .padding()
        .liquidGlassCardStyle()
    }
}

enum TrendDirection {
    case up, down, stable
    
    var iconName: String {
        switch self {
        case .up: return "arrow.up.right"
        case .down: return "arrow.down.right"
        case .stable: return "minus"
        }
    }
    
    var color: Color {
        switch self {
        case .up: return .green
        case .down: return .red
        case .stable: return .gray
        }
    }
}

// MARK: - Recent Activity Section

struct RecentActivitySection: View {
    let vitals: [VitalSignInfo]
    
    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            Text("Recent Activity")
                .font(.headline)
                .padding(.horizontal)
            
            if vitals.isEmpty {
                Text("No recent activity")
                    .font(.subheadline)
                    .foregroundColor(.secondary)
                    .frame(maxWidth: .infinity)
                    .padding()
                    .liquidGlassCardStyle()
                    .padding(.horizontal)
            } else {
                LazyVStack(spacing: 8) {
                    ForEach(vitals.prefix(5)) { vital in
                        RecentActivityRow(vital: vital)
                    }
                }
                .padding(.horizontal)
            }
        }
    }
}

// MARK: - Recent Activity Row

struct RecentActivityRow: View {
    let vital: VitalSignInfo
    
    var body: some View {
        HStack {
            VStack(alignment: .leading, spacing: 2) {
                Text(vital.type.displayName)
                    .font(.subheadline)
                    .fontWeight(.medium)
                
                Text(vital.timestamp.formatted(date: .omitted, time: .shortened))
                    .font(.caption)
                    .foregroundColor(.secondary)
            }
            
            Spacer()
            
            VStack(alignment: .trailing, spacing: 2) {
                Text("\(vital.value, specifier: "%.1f") \(vital.unit)")
                    .font(.subheadline)
                    .fontWeight(.medium)
                
                HStack(spacing: 4) {
                    if !vital.isSynced {
                        Image(systemName: "icloud.and.arrow.up")
                            .font(.caption2)
                            .foregroundColor(.orange)
                    }
                    
                    Text(vital.source)
                        .font(.caption2)
                        .foregroundColor(.secondary)
                }
            }
        }
        .padding(.vertical, 8)
        .padding(.horizontal, 12)
        .liquidGlassCardStyle()
    }
}

// Note: VitalSignsView, ProfileView, and SettingsView are now implemented in separate files

// MARK: - Liquid Glass Style Extensions (iOS 26+ only — no fallbacks)

extension View {
    /// Applies Liquid Glass prominent button style
    func liquidGlassButtonStyle() -> some View {
        self
            .buttonStyle(.glassProminent)
            .glassEffect(.regular.tint(.blue).interactive())
    }
    
    /// Applies Liquid Glass card style
    func liquidGlassCardStyle() -> some View {
        self
            .background {
                RoundedRectangle(cornerRadius: 12)
                    .glassEffect()
            }
    }
    
    /// Applies tinted Liquid Glass card style
    func liquidGlassTintedCard(_ color: Color) -> some View {
        self
            .background {
                RoundedRectangle(cornerRadius: 12)
                    .glassEffect(.regular.tint(color))
            }
    }
    
    /// Applies Liquid Glass navigation bar style
    func liquidGlassNavigationStyle() -> some View {
        self
            .toolbarBackground(.hidden, for: .navigationBar)
            .background {
                VStack {
                    Rectangle()
                        .glassEffect()
                        .frame(height: 100)
                    Spacer()
                }
                .ignoresSafeArea()
            }
    }
    
    /// Applies Liquid Glass tab view style
    func liquidGlassTabViewStyle() -> some View {
        self
            .toolbarBackground(.hidden, for: .tabBar)
            .background {
                VStack {
                    Spacer()
                    Rectangle()
                        .glassEffect(.regular.tint(.primary.opacity(0.03)))
                        .frame(height: 100)
                }
                .ignoresSafeArea()
            }
    }
}

// MARK: - Preview

struct ContentView_Previews: PreviewProvider {
    static var previews: some View {
        ContentView()
            .environmentObject(MobileEngineWrapper())
    }
}