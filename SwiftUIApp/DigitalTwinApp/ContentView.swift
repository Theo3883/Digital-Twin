import SwiftUI

struct ContentView: View {
    @EnvironmentObject var engineWrapper: MobileEngineWrapper
    @State private var selectedTab = 0
    @State private var showSyncGate = true
    
    var body: some View {
        ZStack {
            Group {
                if !engineWrapper.isInitialized {
                    LoadingView(message: "Initializing DigitalTwin...")
                } else if !engineWrapper.isAuthenticated {
                    AuthenticationView()
                } else {
                    MainTabView(selectedTab: $selectedTab)
                }
            }
            
            // Sync Gate overlay
            if showSyncGate && engineWrapper.isAuthenticated {
                SyncGateView(isVisible: $showSyncGate)
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

// MARK: - Sync Gate View

struct SyncGateView: View {
    @EnvironmentObject var engineWrapper: MobileEngineWrapper
    @Binding var isVisible: Bool
    @State private var syncStep = 0 // 0=connecting, 1=auth, 2=ready
    @State private var opacity: Double = 1
    
    private let steps = ["Connecting to cloud…", "Authenticating…", "Loading your data…"]
    
    var body: some View {
        ZStack {
            LiquidGlass.bgDark.ignoresSafeArea()
            
            VStack(spacing: 32) {
                Image(systemName: "heart.text.square.fill")
                    .font(.system(size: 60))
                    .foregroundStyle(LiquidGlass.tealPrimary)
                
                VStack(spacing: 16) {
                    ForEach(0..<steps.count, id: \.self) { index in
                        HStack(spacing: 12) {
                            if index < syncStep {
                                Image(systemName: "checkmark.circle.fill")
                                    .foregroundColor(LiquidGlass.greenPositive)
                            } else if index == syncStep {
                                ProgressView()
                            } else {
                                Image(systemName: "circle")
                                    .foregroundColor(.white.opacity(0.3))
                            }
                            
                            Text(steps[index])
                                .font(.subheadline)
                                .foregroundColor(index <= syncStep ? .white : .white.opacity(0.4))
                            
                            Spacer()
                        }
                        .frame(maxWidth: 280)
                    }
                }
            }
        }
        .opacity(opacity)
        .task {
            // Simulate sync steps
            try? await Task.sleep(for: .milliseconds(400))
            syncStep = 1
            try? await Task.sleep(for: .milliseconds(400))
            syncStep = 2
            
            // Perform actual sync
            let _ = await engineWrapper.performSync()
            await engineWrapper.loadMedications()
            
            try? await Task.sleep(for: .milliseconds(600))
            withAnimation(.easeOut(duration: 0.3)) {
                opacity = 0
            }
            try? await Task.sleep(for: .milliseconds(300))
            isVisible = false
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
                .foregroundColor(.white.opacity(0.65))
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }
}

// MARK: - Authentication View

struct AuthenticationView: View {
    @EnvironmentObject var engineWrapper: MobileEngineWrapper
    @StateObject private var googleSignIn = GoogleSignInService()
    @State private var isAuthenticating = false
    @State private var showRegistrationForm = false
    @State private var googleIdToken: String?
    @State private var errorMessage: String?
    
    // Registration fields
    @State private var googleEmail = ""
    @State private var firstName = ""
    @State private var lastName = ""
    @State private var phone = ""
    @State private var dateOfBirth: Date?
    @State private var city = ""
    @State private var isCreatingAccount = false
    
    var body: some View {
        ScrollView {
            if !showRegistrationForm {
                // Sign-In Screen
                VStack(spacing: 40) {
                    Spacer(minLength: 80)
                    
                    VStack(spacing: 16) {
                        Image(systemName: "heart.text.square.fill")
                            .font(.system(size: 80))
                            .foregroundStyle(LiquidGlass.tealPrimary)
                        
                        Text("DigitalTwin")
                            .font(.largeTitle)
                            .fontWeight(.bold)
                            .foregroundColor(.white)
                        
                        Text("Your Personal Health Companion")
                            .font(.headline)
                            .foregroundColor(.white.opacity(0.65))
                    }
                    
                    Spacer(minLength: 60)
                    
                    VStack(spacing: 16) {
                        Button(action: signInWithGoogle) {
                            HStack(spacing: 10) {
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
                                .foregroundColor(.white.opacity(0.65))
                                .frame(maxWidth: .infinity)
                        }
                        
                        if let error = errorMessage {
                            Text(error)
                                .font(.caption)
                                .foregroundColor(LiquidGlass.redCritical)
                                .multilineTextAlignment(.center)
                        }
                    }
                    
                    Spacer(minLength: 40)
                    
                    Text("By signing in, you agree to our privacy policy and terms of service.")
                        .font(.caption)
                        .foregroundColor(.white.opacity(0.4))
                        .multilineTextAlignment(.center)
                        .padding(.horizontal)
                }
                .padding()
                .pageEnterAnimation()
            } else {
                // Registration Form
                VStack(spacing: 24) {
                    Spacer(minLength: 20)
                    
                    Text("Complete Your Profile")
                        .font(.title2)
                        .fontWeight(.bold)
                        .foregroundColor(.white)
                    
                    Text("Just a few more details to get started")
                        .font(.subheadline)
                        .foregroundColor(.white.opacity(0.65))
                    
                    VStack(spacing: 16) {
                        // Email (read-only)
                        RegistrationField(icon: "envelope.fill", placeholder: "Email") {
                            TextField("Email", text: .constant(googleEmail))
                                .disabled(true)
                                .foregroundColor(.white.opacity(0.5))
                        }
                        
                        // Name row
                        HStack(spacing: 12) {
                            RegistrationField(icon: "person.fill", placeholder: "First Name") {
                                TextField("First Name", text: $firstName)
                                    .foregroundColor(.white)
                            }
                            RegistrationField(icon: "person.fill", placeholder: "Last Name") {
                                TextField("Last Name", text: $lastName)
                                    .foregroundColor(.white)
                            }
                        }
                        
                        // Phone
                        RegistrationField(icon: "phone.fill", placeholder: "Phone Number") {
                            TextField("Phone Number", text: $phone)
                                .keyboardType(.phonePad)
                                .foregroundColor(.white)
                        }
                        
                        // Date of Birth
                        RegistrationField(icon: "calendar", placeholder: "Date of Birth") {
                            DatePicker(
                                "Date of Birth",
                                selection: Binding(
                                    get: { dateOfBirth ?? Date() },
                                    set: { dateOfBirth = $0 }
                                ),
                                in: ...Date(),
                                displayedComponents: .date
                            )
                            .labelsHidden()
                            .colorScheme(.dark)
                        }
                        
                        // City
                        RegistrationField(icon: "mappin.circle.fill", placeholder: "City") {
                            TextField("City", text: $city)
                                .foregroundColor(.white)
                        }
                    }
                    
                    VStack(spacing: 12) {
                        Button(action: createAccount) {
                            HStack {
                                if isCreatingAccount {
                                    ProgressView()
                                        .tint(.white)
                                }
                                Text("Create Account")
                                    .fontWeight(.semibold)
                            }
                            .frame(maxWidth: .infinity)
                            .frame(height: 50)
                        }
                        .liquidGlassButtonStyle()
                        .disabled(firstName.isEmpty || lastName.isEmpty || isCreatingAccount)
                        
                        Button("Cancel") {
                            showRegistrationForm = false
                        }
                        .foregroundColor(.white.opacity(0.65))
                    }
                    
                    Spacer(minLength: 40)
                }
                .padding()
                .pageEnterAnimation()
            }
        }
    }
    
    private func signInWithGoogle() {
        isAuthenticating = true
        errorMessage = nil
        
        Task {
            do {
                let idToken = try await googleSignIn.signIn()
                let success = await engineWrapper.authenticate(googleIdToken: idToken)
                
                isAuthenticating = false
                if success {
                    print("Authentication successful")
                } else {
                    // User needs to register — show form
                    googleIdToken = idToken
                    googleEmail = googleSignIn.userEmail ?? ""
                    firstName = googleSignIn.userGivenName ?? ""
                    lastName = googleSignIn.userFamilyName ?? ""
                    showRegistrationForm = true
                }
            } catch {
                isAuthenticating = false
                errorMessage = error.localizedDescription
            }
        }
    }
    
    private func createAccount() {
        isCreatingAccount = true
        
        Task {
            if let token = googleIdToken {
                let success = await engineWrapper.authenticate(googleIdToken: token)
                isCreatingAccount = false
                if !success {
                    errorMessage = "Failed to create account"
                }
            } else {
                isCreatingAccount = false
                errorMessage = "No authentication token available"
            }
        }
    }
}

// MARK: - Registration Field

struct RegistrationField<Content: View>: View {
    let icon: String
    let placeholder: String
    @ViewBuilder let content: Content
    
    var body: some View {
        HStack(spacing: 12) {
            Image(systemName: icon)
                .foregroundColor(LiquidGlass.tealPrimary)
                .frame(width: 20)
            content
        }
        .padding(14)
        .glassEffect(.regular.tint(.primary.opacity(0.05)), in: RoundedRectangle(cornerRadius: LiquidGlass.radiusInput))
    }
}

// MARK: - Main Tab View (6 pages, 5 tabs + Profile via avatar)

struct MainTabView: View {
    @Binding var selectedTab: Int
    @EnvironmentObject var engineWrapper: MobileEngineWrapper

    var body: some View {
        TabView(selection: $selectedTab) {
            Tab("Home", systemImage: "house.fill", value: 0) {
                DashboardView(selectedTab: $selectedTab)
            }
            Tab("ECG", systemImage: "waveform.path.ecg", value: 1) {
                EcgMonitorView()
            }
            Tab("Assistant", systemImage: "bubble.left.fill", value: 2) {
                MedicalAssistantView()
            }
            Tab("Air", systemImage: "leaf.fill", value: 3) {
                EnvironmentView()
            }
            Tab("Meds", systemImage: "pills.fill", value: 4) {
                MedicationsView()
            }
            Tab("Profile", systemImage: "person.fill", value: 5) {
                ProfileView()
            }
            .defaultVisibility(.hidden, for: .tabBar)
        }
        .tint(LiquidGlass.tealPrimary)
    }
}

// MARK: - Dashboard View (MAUI Home)

struct DashboardView: View {
    @EnvironmentObject var engineWrapper: MobileEngineWrapper
    @Binding var selectedTab: Int
    @State private var recentVitals: [VitalSignInfo] = []
    
    private var latestHR: Double {
        recentVitals.first(where: { $0.type == .heartRate })?.value ?? 0
    }
    private var latestSpO2: Double {
        recentVitals.first(where: { $0.type == .oxygenSaturation })?.value ?? 0
    }
    private var latestSteps: Double {
        recentVitals.first(where: { $0.type == .stepCount })?.value ?? 0
    }
    private var sleepMinutes: Int {
        guard let session = engineWrapper.sleepSessions.first else { return 0 }
        return session.durationMinutes
    }
    private var sleepQuality: Double {
        engineWrapper.sleepSessions.first?.qualityScore ?? 0
    }
    private var hasProfile: Bool {
        engineWrapper.patientProfile != nil
    }
    
    var body: some View {
        ZStack(alignment: .top) {
            ScrollView {
                VStack(spacing: 0) {
                    Spacer().frame(height: 80)
                    
                    HomeWidgetGrid(
                        heartRate: latestHR,
                        spO2: latestSpO2,
                        steps: latestSteps,
                        envReading: engineWrapper.latestEnvironmentReading,
                        sleepMinutes: sleepMinutes,
                        sleepQuality: sleepQuality,
                        insightText: engineWrapper.coachingAdvice?.advice,
                        hasProfile: hasProfile,
                        selectedTab: $selectedTab
                    )
                    .padding(.horizontal, 16)
                    .padding(.bottom, 32)
                }
            }
            
            HomeTopBar(user: engineWrapper.currentUser) {
                selectedTab = 5
            }
        }
        .pageEnterAnimation()
        .task {
            await loadDashboardData()
        }
        .refreshable {
            await loadDashboardData()
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

// MARK: - Home Top Bar

struct HomeTopBar: View {
    let user: UserInfo?
    var onAvatarTap: () -> Void = {}
    
    private var greeting: String {
        let h = Calendar.current.component(.hour, from: Date())
        if h < 12 { return "Good Morning" }
        if h < 18 { return "Good Afternoon" }
        return "Good Evening"
    }
    
    private var userName: String {
        user?.displayName ?? "User"
    }
    
    private var userInitials: String {
        userName.split(separator: " ")
            .prefix(2)
            .compactMap { $0.first.map(String.init) }
            .joined()
            .uppercased()
    }
    
    var body: some View {
        HStack {
            HStack(spacing: 12) {
                // Avatar with glass circle
                Button { onAvatarTap() } label: {
                    if let photoUrl = user?.photoUrl, let url = URL(string: photoUrl) {
                        AsyncImage(url: url) { image in
                            image.resizable().aspectRatio(contentMode: .fill)
                        } placeholder: {
                            avatarPlaceholder
                        }
                        .frame(width: 40, height: 40)
                        .clipShape(Circle())
                        .glassEffect(.regular, in: Circle())
                    } else {
                        avatarPlaceholder
                            .glassEffect(.regular, in: Circle())
                    }
                }
                .buttonStyle(.plain)
                
                VStack(alignment: .leading, spacing: 2) {
                    Text(userName)
                        .font(.system(size: 18, weight: .semibold))
                        .foregroundColor(.white)
                    Text(greeting)
                        .font(.system(size: 12))
                        .foregroundColor(.white.opacity(0.65))
                }
            }
            
            Spacer()
            
            // Notification bell
            Button(action: {}) {
                ZStack(alignment: .topTrailing) {
                    Image(systemName: "bell.fill")
                        .font(.system(size: 18))
                        .foregroundColor(.white)
                    
                    Circle()
                        .fill(LiquidGlass.redCritical)
                        .frame(width: 8, height: 8)
                        .offset(x: 2, y: -2)
                }
            }
            .glassPill()
        }
        .padding(.horizontal, 16)
        .padding(.vertical, 12)
        .glassEffect(.regular, in: RoundedRectangle(cornerRadius: LiquidGlass.radiusCard))
        .padding(.horizontal, 8)
        .padding(.top, 4)
    }
    
    private var avatarPlaceholder: some View {
        ZStack {
            Circle()
                .fill(LinearGradient(colors: [Color(red: 96/255, green: 165/255, blue: 250/255), Color(red: 168/255, green: 85/255, blue: 247/255)], startPoint: .topLeading, endPoint: .bottomTrailing))
                .frame(width: 40, height: 40)
            Text(userInitials)
                .font(.system(size: 14, weight: .bold))
                .foregroundColor(.white)
        }
    }
}

// MARK: - Home Widget Grid

struct HomeWidgetGrid: View {
    let heartRate: Double
    let spO2: Double
    let steps: Double
    let envReading: EnvironmentReadingInfo?
    let sleepMinutes: Int
    let sleepQuality: Double
    let insightText: String?
    let hasProfile: Bool
    @Binding var selectedTab: Int
    
    private var spO2Status: String {
        if spO2 >= 95 { return "Normal" }
        if spO2 >= 90 { return "Low" }
        return "Critical"
    }
    
    private var spO2StatusColor: Color {
        if spO2 >= 95 { return LiquidGlass.greenPositive }
        if spO2 >= 90 { return LiquidGlass.amberWarning }
        return LiquidGlass.redCritical
    }
    
    private var sleepQualityLabel: String {
        if sleepQuality >= 80 { return "Optimal" }
        if sleepQuality >= 60 { return "Fair" }
        return "Poor"
    }
    
    var body: some View {
        let columns = [GridItem(.flexible(), spacing: 12), GridItem(.flexible(), spacing: 12)]
        
        VStack(spacing: 12) {
            // 1. Heart Rate Hero Card (full width)
            heartRateCard
            
            // 2×2 grid: Steps, SpO2, Environment, Sleep
            LazyVGrid(columns: columns, spacing: 12) {
                stepsCard
                spO2Card
                environmentCard
                sleepCard
            }
        }
        
        // AI Insight Hero (full width)
        aiInsightHeroCard
    }
    
    // MARK: Heart Rate Hero
    
    private var heartRateCard: some View {
        ZStack(alignment: .bottom) {
            VStack(alignment: .leading, spacing: 12) {
                HStack {
                    ZStack {
                        Circle()
                            .fill(LiquidGlass.redCritical.opacity(0.2))
                            .frame(width: 32, height: 32)
                        Image(systemName: "heart.fill")
                            .font(.system(size: 14))
                            .foregroundColor(LiquidGlass.redCritical)
                    }
                    Text("Heart Rate")
                        .font(.subheadline)
                        .foregroundColor(.white.opacity(0.65))
                    Spacer()
                    if hasProfile {
                        Text("Live")
                            .font(.caption2.weight(.semibold))
                            .foregroundColor(LiquidGlass.greenPositive)
                            .padding(.horizontal, 8)
                            .padding(.vertical, 3)
                            .background {
                                RoundedRectangle(cornerRadius: LiquidGlass.radiusChip)
                                    .fill(LiquidGlass.greenPositive.opacity(0.15))
                            }
                    }
                }
                
                if hasProfile {
                    HStack(alignment: .firstTextBaseline, spacing: 4) {
                        Text(heartRate > 0 ? String(format: "%.0f", heartRate) : "--")
                            .font(.system(size: 56, weight: .bold, design: .rounded))
                            .foregroundColor(.white)
                        Text("BPM")
                            .font(.caption)
                            .foregroundColor(.white.opacity(0.4))
                    }
                    
                    Text(heartRate > 0 ? "\(String(format: "%.0f", heartRate)) BPM · Live" : "Waiting for data…")
                        .font(.caption.weight(.medium))
                        .foregroundColor(LiquidGlass.redCritical.opacity(0.8))
                } else {
                    Button {
                        selectedTab = 5
                    } label: {
                        HStack(spacing: 8) {
                            Image(systemName: "person.crop.circle.badge.plus")
                                .font(.title2)
                                .foregroundColor(.white.opacity(0.4))
                            Text("Set up your patient profile")
                                .font(.subheadline)
                                .foregroundColor(.white.opacity(0.5))
                            Spacer()
                            Image(systemName: "chevron.right")
                                .font(.caption)
                                .foregroundColor(.white.opacity(0.3))
                        }
                    }
                    .padding(.vertical, 8)
                }
            }
            .frame(maxWidth: .infinity, alignment: .leading)
            .zIndex(1)
            
            // ECG sparkline decoration
            if hasProfile {
                EcgSparkline()
                    .frame(height: 96)
                    .opacity(0.3)
            }
        }
        .glassCard(tint: LiquidGlass.redCritical.opacity(0.08))
    }
    
    // MARK: Steps
    
    private var stepsCard: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack {
                ZStack {
                    Circle()
                        .fill(LiquidGlass.amberWarning.opacity(0.2))
                        .frame(width: 32, height: 32)
                    Image(systemName: "figure.walk")
                        .font(.system(size: 14))
                        .foregroundColor(LiquidGlass.amberWarning)
                }
                Spacer()
            }
            Text("Steps")
                .font(.caption)
                .foregroundColor(.white.opacity(0.65))
            Text(steps > 0 ? String(format: "%.0f", steps) : "--")
                .font(.title2.weight(.bold))
                .foregroundColor(.white)
            // Progress bar
            GeometryReader { geo in
                ZStack(alignment: .leading) {
                    Capsule()
                        .fill(.white.opacity(0.1))
                        .frame(height: 4)
                    Capsule()
                        .fill(LiquidGlass.amberWarning)
                        .frame(width: geo.size.width * min(steps / 10000, 1), height: 4)
                }
            }
            .frame(height: 4)
        }
        .frame(minHeight: 140)
        .glassCard()
    }
    
    // MARK: SpO2
    
    private var spO2Card: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack {
                ZStack {
                    Circle()
                        .fill(LiquidGlass.bluePrimary.opacity(0.2))
                        .frame(width: 32, height: 32)
                    Image(systemName: "lungs.fill")
                        .font(.system(size: 14))
                        .foregroundColor(LiquidGlass.bluePrimary)
                }
                Spacer()
            }
            Text("Blood Oxygen")
                .font(.caption)
                .foregroundColor(.white.opacity(0.65))
            Text(spO2 > 0 ? String(format: "%.1f%%", spO2) : "--")
                .font(.title2.weight(.bold))
                .foregroundColor(.white)
            Text(spO2 > 0 ? spO2Status : "No data")
                .font(.caption2)
                .foregroundColor(spO2 > 0 ? spO2StatusColor : .white.opacity(0.4))
        }
        .frame(minHeight: 140)
        .glassCard()
    }
    
    // MARK: Environment
    
    private var environmentCard: some View {
        VStack(alignment: .leading, spacing: 8) {
            HStack {
                ZStack {
                    Circle()
                        .fill(LiquidGlass.greenPositive.opacity(0.2))
                        .frame(width: 32, height: 32)
                    Image(systemName: "location.fill")
                        .font(.system(size: 14))
                        .foregroundColor(LiquidGlass.greenPositive)
                }
                Spacer()
            }
            Text(envReading?.locationDisplayName ?? "Unknown")
                .font(.caption)
                .foregroundColor(.white.opacity(0.65))
            if let temp = envReading?.temperature {
                Text(String(format: "%.0f°", temp))
                    .font(.title2.weight(.bold))
                    .foregroundColor(.white)
            } else {
                Text("--")
                    .font(.title2.weight(.bold))
                    .foregroundColor(.white.opacity(0.4))
            }
            if let aqi = envReading?.aqiIndex {
                Text("AQI \(aqi) · \(envReading?.airQualityDisplay ?? "")")
                    .font(.caption2)
                    .foregroundColor(.white.opacity(0.5))
            } else {
                Text("No data")
                    .font(.caption2)
                    .foregroundColor(.white.opacity(0.4))
            }
        }
        .frame(minHeight: 140)
        .glassCard(tint: LiquidGlass.greenPositive.opacity(0.08))
    }
    
    // MARK: Sleep
    
    private var sleepCard: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack {
                ZStack {
                    Circle()
                        .fill(Color(red: 99/255, green: 102/255, blue: 241/255).opacity(0.2))
                        .frame(width: 32, height: 32)
                    Image(systemName: "moon.fill")
                        .font(.system(size: 14))
                        .foregroundColor(LiquidGlass.purpleSleep)
                }
                Spacer()
            }
            Text("Sleep")
                .font(.caption)
                .foregroundColor(.white.opacity(0.65))
            if sleepMinutes > 0 {
                Text("\(sleepMinutes / 60)h \(sleepMinutes % 60)m")
                    .font(.title2.weight(.bold))
                    .foregroundColor(.white)
                Text(sleepQualityLabel)
                    .font(.caption2)
                    .foregroundColor(LiquidGlass.purpleSleep)
            } else {
                Text("--")
                    .font(.title2.weight(.bold))
                    .foregroundColor(.white.opacity(0.4))
                Text("No data")
                    .font(.caption2)
                    .foregroundColor(.white.opacity(0.4))
            }
        }
        .frame(minHeight: 140)
        .glassCard()
    }
    
    // MARK: AI Insight Hero
    
    private var aiInsightHeroCard: some View {
        HStack(spacing: 12) {
            ZStack {
                Circle()
                    .fill(LiquidGlass.tealPrimary.opacity(0.15))
                    .frame(width: 36, height: 36)
                Image(systemName: "sparkle")
                    .font(.system(size: 16))
                    .foregroundColor(LiquidGlass.tealPrimary)
            }
            
            VStack(alignment: .leading, spacing: 4) {
                Text("MedAssist Insights")
                    .font(.subheadline.weight(.semibold))
                    .foregroundColor(.white)
                Text(insightText ?? "Tap to chat with your health assistant.")
                    .font(.caption)
                    .foregroundColor(.white.opacity(0.65))
                    .lineLimit(2)
            }
            
            Spacer()
            
            Image(systemName: "chevron.right")
                .font(.caption)
                .foregroundColor(.white.opacity(0.3))
        }
        .frame(maxWidth: .infinity)
        .glassCard(tint: LiquidGlass.tealPrimary.opacity(0.06))
        .padding(.top, 0)
    }
}

// MARK: - ECG Sparkline (decorative waveform)

struct EcgSparkline: View {
    var body: some View {
        GeometryReader { geo in
            Path { path in
                let w = geo.size.width
                let h = geo.size.height
                // Same shape as the MAUI SVG: M0,20 L10,20 L15,5 L20,25 L25,20 L40,20 L45,10 L50,28 L55,20 L100,20
                let points: [(CGFloat, CGFloat)] = [
                    (0, 0.67), (0.10, 0.67), (0.15, 0.17), (0.20, 0.83),
                    (0.25, 0.67), (0.40, 0.67), (0.45, 0.33), (0.50, 0.93),
                    (0.55, 0.67), (1.0, 0.67)
                ]
                path.move(to: CGPoint(x: points[0].0 * w, y: points[0].1 * h))
                for pt in points.dropFirst() {
                    path.addLine(to: CGPoint(x: pt.0 * w, y: pt.1 * h))
                }
            }
            .stroke(LiquidGlass.redCritical, lineWidth: 2)
        }
    }
}

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
            .glassEffect(.regular, in: RoundedRectangle(cornerRadius: LiquidGlass.radiusCard))
    }
    
    /// Applies tinted Liquid Glass card style
    func liquidGlassTintedCard(_ color: Color) -> some View {
        self
            .glassEffect(.regular.tint(color), in: RoundedRectangle(cornerRadius: LiquidGlass.radiusCard))
    }
    
    /// Applies Liquid Glass navigation bar style
    func liquidGlassNavigationStyle() -> some View {
        self
            .toolbarBackground(.hidden, for: .navigationBar)
    }
    
    /// Applies Liquid Glass tab view style
    func liquidGlassTabViewStyle() -> some View {
        self
            .tabViewStyle(.tabBarOnly)
            .toolbarBackground(.hidden, for: .tabBar)
    }
}

// MARK: - Preview

struct ContentView_Previews: PreviewProvider {
    static var previews: some View {
        ContentView()
            .environmentObject(MobileEngineWrapper())
    }
}