import SwiftUI

struct SettingsView: View {
    @EnvironmentObject var engineWrapper: MobileEngineWrapper
    @State private var showingSignOutAlert = false
    @State private var notificationsEnabled = true
    @State private var healthKitEnabled = true
    @State private var backgroundSyncEnabled = true
    @State private var biometricAuthEnabled = false
    
    var body: some View {
        NavigationView {
            List {
                // User Section
                if let user = engineWrapper.currentUser {
                    UserSection(user: user)
                }
                
                // Sync & Data Section
                SyncDataSection(
                    backgroundSyncEnabled: $backgroundSyncEnabled,
                    healthKitEnabled: $healthKitEnabled
                )
                
                // Privacy & Security Section
                PrivacySecuritySection(
                    notificationsEnabled: $notificationsEnabled,
                    biometricAuthEnabled: $biometricAuthEnabled
                )
                
                // App Information Section
                AppInfoSection()

                DatabasePathRow(path: engineWrapper.databasePathString)
                
                // Account Actions Section
                AccountActionsSection(showingSignOutAlert: $showingSignOutAlert)
            }
            .scrollIndicators(.hidden)
            .listStyle(.insetGrouped)
            .navigationTitle("Settings")
            .liquidGlassNavigationStyle()
            .alert("Sign Out", isPresented: $showingSignOutAlert) {
                Button("Cancel", role: .cancel) { }
                Button("Sign Out", role: .destructive) {
                    signOut()
                }
            } message: {
                Text("Are you sure you want to sign out? Your local data will remain on this device.")
            }
        }
    }
    
    private func signOut() {
        // Sign out logic would go here
        print("Signing out...")
    }
}

// MARK: - User Section

struct UserSection: View {
    let user: UserInfo
    
    var body: some View {
        Section {
            HStack(spacing: 12) {
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
                
                VStack(alignment: .leading, spacing: 2) {
                    Text(user.displayName)
                        .font(.headline)
                    
                    Text(user.email)
                        .font(.subheadline)
                        .foregroundColor(.secondary)
                }
                
                Spacer()
            }
            .padding(.vertical, 4)
        }
    }
}

// MARK: - Sync & Data Section

struct SyncDataSection: View {
    @EnvironmentObject var engineWrapper: MobileEngineWrapper
    @Binding var backgroundSyncEnabled: Bool
    @Binding var healthKitEnabled: Bool
    @State private var lastSyncDate: Date?
    @State private var isSyncing = false
    
    var body: some View {
        Section("Sync & Data") {
            // Manual Sync
            HStack {
                Image(systemName: "icloud.and.arrow.up.and.down")
                    .foregroundColor(.blue)
                    .frame(width: 20)
                
                VStack(alignment: .leading, spacing: 2) {
                    Text("Sync Now")
                        .font(.subheadline)
                    
                    if let lastSync = lastSyncDate {
                        Text("Last sync: \(lastSync.formatted(date: .omitted, time: .shortened))")
                            .font(.caption)
                            .foregroundColor(.secondary)
                    } else {
                        Text("Never synced")
                            .font(.caption)
                            .foregroundColor(.secondary)
                    }
                }
                
                Spacer()
                
                if isSyncing {
                    ProgressView()
                        .scaleEffect(0.8)
                } else {
                    Button("Sync") {
                        Task { await performSync() }
                    }
                    .liquidGlassButtonStyle()
                }
            }
            
            // Background Sync Toggle
            HStack {
                Image(systemName: "arrow.triangle.2.circlepath")
                    .foregroundColor(.green)
                    .frame(width: 20)
                
                VStack(alignment: .leading, spacing: 2) {
                    Text("Background Sync")
                        .font(.subheadline)
                    
                    Text("Automatically sync when app is in background")
                        .font(.caption)
                        .foregroundColor(.secondary)
                }
                
                Spacer()
                
                Toggle("", isOn: $backgroundSyncEnabled)
            }
            
            // HealthKit Integration
            HStack {
                Image(systemName: "heart.fill")
                    .foregroundColor(.red)
                    .frame(width: 20)
                
                VStack(alignment: .leading, spacing: 2) {
                    Text("HealthKit Integration")
                        .font(.subheadline)
                    
                    Text("Read and write health data to HealthKit")
                        .font(.caption)
                        .foregroundColor(.secondary)
                }
                
                Spacer()
                
                Toggle("", isOn: $healthKitEnabled)
            }
            
            // Data Usage
            NavigationLink(destination: DataUsageView()) {
                HStack {
                    Image(systemName: "chart.bar.fill")
                        .foregroundColor(.purple)
                        .frame(width: 20)
                    
                    Text("Data Usage")
                        .font(.subheadline)
                }
            }
        }
    }
    
    private func performSync() async {
        isSyncing = true
        
        let success = await engineWrapper.performSync()
        
        await MainActor.run {
            isSyncing = false
            if success {
                lastSyncDate = Date()
            }
        }
    }
}

// MARK: - Privacy & Security Section

struct PrivacySecuritySection: View {
    @Binding var notificationsEnabled: Bool
    @Binding var biometricAuthEnabled: Bool
    
    var body: some View {
        Section("Privacy & Security") {
            // Notifications
            HStack {
                Image(systemName: "bell.fill")
                    .foregroundColor(.orange)
                    .frame(width: 20)
                
                VStack(alignment: .leading, spacing: 2) {
                    Text("Notifications")
                        .font(.subheadline)
                    
                    Text("Health reminders and sync updates")
                        .font(.caption)
                        .foregroundColor(.secondary)
                }
                
                Spacer()
                
                Toggle("", isOn: $notificationsEnabled)
            }
            
            // Biometric Authentication
            HStack {
                Image(systemName: "faceid")
                    .foregroundColor(.blue)
                    .frame(width: 20)
                
                VStack(alignment: .leading, spacing: 2) {
                    Text("Biometric Authentication")
                        .font(.subheadline)
                    
                    Text("Use Face ID or Touch ID to unlock app")
                        .font(.caption)
                        .foregroundColor(.secondary)
                }
                
                Spacer()
                
                Toggle("", isOn: $biometricAuthEnabled)
            }
            
            // Privacy Policy
            NavigationLink(destination: PrivacyPolicyView()) {
                HStack {
                    Image(systemName: "hand.raised.fill")
                        .foregroundColor(.green)
                        .frame(width: 20)
                    
                    Text("Privacy Policy")
                        .font(.subheadline)
                }
            }
            
            // Data Export
            NavigationLink(destination: DataExportView()) {
                HStack {
                    Image(systemName: "square.and.arrow.up")
                        .foregroundColor(.blue)
                        .frame(width: 20)
                    
                    Text("Export My Data")
                        .font(.subheadline)
                }
            }
        }
    }
}

// MARK: - App Information Section

struct AppInfoSection: View {
    var body: some View {
        Section("App Information") {
            // Version
            HStack {
                Image(systemName: "info.circle")
                    .foregroundColor(.gray)
                    .frame(width: 20)
                
                Text("Version")
                    .font(.subheadline)
                
                Spacer()
                
                Text("1.0.0")
                    .font(.subheadline)
                    .foregroundColor(.secondary)
            }
            
            // Build
            HStack {
                Image(systemName: "hammer")
                    .foregroundColor(.gray)
                    .frame(width: 20)
                
                Text("Build")
                    .font(.subheadline)
                
                Spacer()
                
                Text("1")
                    .font(.subheadline)
                    .foregroundColor(.secondary)
            }
            
            // Support
            NavigationLink(destination: SupportView()) {
                HStack {
                    Image(systemName: "questionmark.circle")
                        .foregroundColor(.blue)
                        .frame(width: 20)
                    
                    Text("Help & Support")
                        .font(.subheadline)
                }
            }
            
            // Terms of Service
            NavigationLink(destination: TermsOfServiceView()) {
                HStack {
                    Image(systemName: "doc.text")
                        .foregroundColor(.gray)
                        .frame(width: 20)
                    
                    Text("Terms of Service")
                        .font(.subheadline)
                }
            }
        }
    }
}

// MARK: - Account Actions Section

struct AccountActionsSection: View {
    @Binding var showingSignOutAlert: Bool
    
    var body: some View {
        Section {
            // Sign Out
            Button(action: { showingSignOutAlert = true }) {
                HStack {
                    Image(systemName: "rectangle.portrait.and.arrow.right")
                        .foregroundColor(.red)
                        .frame(width: 20)
                    
                    Text("Sign Out")
                        .font(.subheadline)
                        .foregroundColor(.red)
                }
            }
            
            // Delete Account
            NavigationLink(destination: DeleteAccountView()) {
                HStack {
                    Image(systemName: "trash")
                        .foregroundColor(.red)
                        .frame(width: 20)
                    
                    Text("Delete Account")
                        .font(.subheadline)
                        .foregroundColor(.red)
                }
            }
        }
    }
}

// MARK: - Placeholder Detail Views

struct DataUsageView: View {
    @EnvironmentObject private var engineWrapper: MobileEngineWrapper
    @State private var dbSize: String = "—"
    @State private var lastSyncDate: String = "Never"
    @State private var showResetAlert = false
    @State private var isResetting = false

    var body: some View {
        ScrollView(showsIndicators: false) {
            VStack(spacing: 16) {
                // Database size
                HStack {
                    Image(systemName: "internaldrive.fill")
                        .foregroundColor(LiquidGlass.tealPrimary)
                    Text("Local Database")
                        .font(.subheadline.weight(.medium))
                        .foregroundColor(.white)
                    Spacer()
                    Text(dbSize)
                        .font(.caption.monospacedDigit())
                        .foregroundColor(.white.opacity(0.5))
                }
                .glassCard()

                // Sync status
                HStack {
                    Image(systemName: "arrow.triangle.2.circlepath")
                        .foregroundColor(LiquidGlass.tealPrimary)
                    Text("Last Sync")
                        .font(.subheadline.weight(.medium))
                        .foregroundColor(.white)
                    Spacer()
                    Text(lastSyncDate)
                        .font(.caption)
                        .foregroundColor(.white.opacity(0.5))
                }
                .glassCard()

                // Records count
                VStack(alignment: .leading, spacing: 8) {
                    Text("Stored Records")
                        .font(.subheadline.weight(.medium))
                        .foregroundColor(.white)
                    HStack(spacing: 16) {
                        dataCountBadge("Medications", count: engineWrapper.medications.count)
                        dataCountBadge("Chats", count: engineWrapper.chatMessages.count)
                        dataCountBadge("Documents", count: engineWrapper.ocrDocuments.count)
                    }
                }
                .glassCard()

                // Reset Data
                Button(role: .destructive) {
                    showResetAlert = true
                } label: {
                    HStack {
                        Image(systemName: "trash.fill")
                        Text(isResetting ? "Resetting..." : "Reset All Local Data")
                    }
                    .font(.subheadline.weight(.medium))
                    .foregroundColor(LiquidGlass.redCritical)
                    .frame(maxWidth: .infinity)
                    .padding(.vertical, 12)
                }
                .glassCard()
                .disabled(isResetting)
            }
            .padding(16)
        }
        .navigationTitle("Data Usage")
        .liquidGlassNavigationStyle()
        .task { loadStats() }
        .alert("Reset All Data?", isPresented: $showResetAlert) {
            Button("Reset", role: .destructive) {
                Task {
                    isResetting = true
                    _ = await engineWrapper.resetLocalData()
                    isResetting = false
                    loadStats()
                }
            }
            Button("Cancel", role: .cancel) {}
        } message: {
            Text("This will permanently delete all local data including vitals, medications, chat history, and documents. Cloud data is not affected.")
        }
    }

    private func loadStats() {
        let path = engineWrapper.databasePathString
        if let attrs = try? FileManager.default.attributesOfItem(atPath: path),
           let size = attrs[.size] as? Int64 {
            let formatter = ByteCountFormatter()
            formatter.allowedUnits = [.useKB, .useMB]
            formatter.countStyle = .file
            dbSize = formatter.string(fromByteCount: size)
        }
    }

    private func dataCountBadge(_ label: String, count: Int) -> some View {
        VStack(spacing: 2) {
            Text("\(count)")
                .font(.system(size: 18, weight: .semibold, design: .rounded))
                .foregroundColor(.white)
            Text(label)
                .font(.caption2)
                .foregroundColor(.white.opacity(0.4))
        }
        .frame(maxWidth: .infinity)
    }
}

struct PrivacyPolicyView: View {
    var body: some View {
        ScrollView(showsIndicators: false) {
            VStack(alignment: .leading, spacing: 16) {
                Text("Privacy Policy")
                    .font(.title)
                    .fontWeight(.bold)
                
                Text("Your privacy is important to us. This privacy policy explains how we collect, use, and protect your personal health information.")
                    .font(.body)
                
                Text("Data Collection")
                    .font(.headline)
                    .padding(.top)
                
                Text("We collect health data that you voluntarily provide through the app, including vital signs, medical history, and profile information.")
                    .font(.body)
                
                Text("Data Usage")
                    .font(.headline)
                    .padding(.top)
                
                Text("Your health data is used solely for providing personalized health insights and syncing across your devices.")
                    .font(.body)
                
                Text("Data Security")
                    .font(.headline)
                    .padding(.top)
                
                Text("All data is encrypted in transit and at rest. We use industry-standard security measures to protect your information.")
                    .font(.body)
            }
            .padding()
        }
        .navigationTitle("Privacy Policy")
        .liquidGlassNavigationStyle()
    }
}

struct DataExportView: View {
    @EnvironmentObject private var engineWrapper: MobileEngineWrapper
    @State private var isExporting = false
    @State private var exportURL: URL?
    @State private var showShareSheet = false
    @State private var exportError: String?

    var body: some View {
        VStack(spacing: 20) {
            Image(systemName: "square.and.arrow.up.fill")
                .font(.system(size: 48))
                .foregroundColor(LiquidGlass.tealPrimary)

            Text("Export Your Data")
                .font(.title3.weight(.semibold))
                .foregroundColor(.white)

            Text("Download a copy of all your health data in JSON format. This includes your profile, vitals, medications, and medical history.")
                .font(.caption)
                .multilineTextAlignment(.center)
                .foregroundColor(.white.opacity(0.5))

            if let error = exportError {
                Text(error)
                    .font(.caption)
                    .foregroundColor(LiquidGlass.redCritical)
            }

            Button {
                Task { await exportData() }
            } label: {
                HStack {
                    if isExporting {
                        ProgressView().tint(.white)
                    }
                    Text(isExporting ? "Exporting..." : "Export Data")
                }
            }
            .liquidGlassButtonStyle()
            .disabled(isExporting)
        }
        .padding()
        .navigationTitle("Data Export")
        .liquidGlassNavigationStyle()
        .sheet(isPresented: $showShareSheet) {
            if let url = exportURL {
                ShareSheet(items: [url])
            }
        }
    }

    private func exportData() async {
        isExporting = true
        exportError = nil
        defer { isExporting = false }

        let encoder = JSONEncoder()
        encoder.dateEncodingStrategy = .iso8601
        encoder.outputFormatting = [.prettyPrinted, .sortedKeys]

        struct ExportData: Codable {
            let exportDate: Date
            let profile: PatientInfo?
            let medications: [MedicationInfo]
            let medicalHistory: [MedicalHistoryEntryInfo]
            let sleepSessions: [SleepSessionInfo]
        }

        let data = ExportData(
            exportDate: Date(),
            profile: engineWrapper.patientProfile,
            medications: engineWrapper.medications,
            medicalHistory: engineWrapper.medicalHistory,
            sleepSessions: engineWrapper.sleepSessions
        )

        do {
            let jsonData = try encoder.encode(data)
            let tempDir = FileManager.default.temporaryDirectory
            let fileName = "digitaltwin-export-\(ISO8601DateFormatter().string(from: Date())).json"
            let fileURL = tempDir.appendingPathComponent(fileName)
            try jsonData.write(to: fileURL)
            exportURL = fileURL
            showShareSheet = true
        } catch {
            exportError = "Export failed: \(error.localizedDescription)"
        }
    }
}

struct ShareSheet: UIViewControllerRepresentable {
    let items: [Any]

    func makeUIViewController(context: Context) -> UIActivityViewController {
        UIActivityViewController(activityItems: items, applicationActivities: nil)
    }

    func updateUIViewController(_ uiViewController: UIActivityViewController, context: Context) {}
}

struct SupportView: View {
    var body: some View {
        List {
            Section("Contact") {
                HStack {
                    Image(systemName: "envelope")
                    Text("Email Support")
                    Spacer()
                    Text("support@digitaltwin.com")
                        .foregroundColor(.secondary)
                }
            }
            
            Section("Resources") {
                NavigationLink("FAQ", destination: Text("FAQ"))
                NavigationLink("User Guide", destination: Text("User Guide"))
                NavigationLink("Video Tutorials", destination: Text("Video Tutorials"))
            }
        }
        .scrollIndicators(.hidden)
        .navigationTitle("Help & Support")
        .liquidGlassNavigationStyle()
    }
}

struct TermsOfServiceView: View {
    var body: some View {
        ScrollView(showsIndicators: false) {
            VStack(alignment: .leading, spacing: 16) {
                Text("Terms of Service")
                    .font(.title)
                    .fontWeight(.bold)
                
                Text("By using DigitalTwin, you agree to these terms of service.")
                    .font(.body)
                
                Text("Acceptance of Terms")
                    .font(.headline)
                    .padding(.top)
                
                Text("By accessing and using this app, you accept and agree to be bound by the terms and provision of this agreement.")
                    .font(.body)
                
                Text("Use License")
                    .font(.headline)
                    .padding(.top)
                
                Text("Permission is granted to temporarily use DigitalTwin for personal, non-commercial transitory viewing only.")
                    .font(.body)
            }
            .padding()
        }
        .navigationTitle("Terms of Service")
        .liquidGlassNavigationStyle()
    }
}

struct DeleteAccountView: View {
    @State private var showingDeleteAlert = false
    
    var body: some View {
        VStack(spacing: 20) {
            Image(systemName: "exclamationmark.triangle.fill")
                .font(.system(size: 60))
                .foregroundColor(.red)
            
            Text("Delete Account")
                .font(.title)
                .fontWeight(.bold)
            
            Text("This action cannot be undone. All your data will be permanently deleted from our servers.")
                .multilineTextAlignment(.center)
                .foregroundColor(.secondary)
            
            Button("Delete My Account") {
                showingDeleteAlert = true
            }
            .foregroundColor(.white)
            .padding()
            .background(Color.red)
            .cornerRadius(12)
        }
        .padding()
        .navigationTitle("Delete Account")
        .liquidGlassNavigationStyle()
        .alert("Confirm Deletion", isPresented: $showingDeleteAlert) {
            Button("Cancel", role: .cancel) { }
            Button("Delete", role: .destructive) {
                // Delete account logic
            }
        } message: {
            Text("Are you absolutely sure? This action cannot be undone.")
        }
    }
}

struct SettingsView_Previews: PreviewProvider {
    static var previews: some View {
        SettingsView()
            .environmentObject(MobileEngineWrapper())
    }
}