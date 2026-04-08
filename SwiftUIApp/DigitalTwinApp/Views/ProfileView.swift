import SwiftUI

struct ProfileView: View {
    @EnvironmentObject var engineWrapper: MobileEngineWrapper
    @State private var isEditing = false
    @State private var showingImagePicker = false
    
    var body: some View {
        NavigationView {
            ScrollView {
                VStack(spacing: 24) {
                    // Profile Header
                    ProfileHeaderView(
                        user: engineWrapper.currentUser,
                        patient: engineWrapper.patientProfile,
                        isEditing: $isEditing,
                        showingImagePicker: $showingImagePicker
                    )
                    
                    // Patient Information
                    if let patient = engineWrapper.patientProfile {
                        PatientInfoSection(patient: patient, isEditing: isEditing)
                    }
                    
                    // Health Metrics
                    if let patient = engineWrapper.patientProfile {
                        HealthMetricsSection(patient: patient, isEditing: isEditing)
                    }
                    
                    // Medical Information
                    if let patient = engineWrapper.patientProfile {
                        MedicalInfoSection(patient: patient, isEditing: isEditing)
                    }
                    
                    Spacer(minLength: 100) // Tab bar spacing
                }
                .padding()
            }
            .navigationTitle("Profile")
            .liquidGlassNavigationStyle()
            .toolbar {
                ToolbarItem(placement: .navigationBarTrailing) {
                    Button(isEditing ? "Done" : "Edit") {
                        if isEditing {
                            Task { await saveProfile() }
                        }
                        isEditing.toggle()
                    }
                    .liquidGlassButtonStyle()
                }
            }
            .task {
                await loadProfile()
            }
        }
    }
    
    private func loadProfile() async {
        await engineWrapper.getCurrentUser()
        await engineWrapper.loadPatientProfile()
    }
    
    private func saveProfile() async {
        // Profile saving logic would go here
        // For now, just toggle editing mode
        print("Saving profile...")
    }
}

// MARK: - Profile Header

struct ProfileHeaderView: View {
    let user: UserInfo?
    let patient: PatientInfo?
    @Binding var isEditing: Bool
    @Binding var showingImagePicker: Bool
    
    var body: some View {
        VStack(spacing: 16) {
            // Profile Photo
            Button(action: { if isEditing { showingImagePicker = true } }) {
                AsyncImage(url: user?.photoUrl.flatMap(URL.init)) { image in
                    image
                        .resizable()
                        .aspectRatio(contentMode: .fill)
                } placeholder: {
                    Image(systemName: "person.circle.fill")
                        .font(.system(size: 80))
                        .foregroundColor(.blue)
                }
                .frame(width: 100, height: 100)
                .clipShape(Circle())
                .overlay(
                    Circle()
                        .stroke(Color.blue, lineWidth: 3)
                )
                .overlay(
                    Group {
                        if isEditing {
                            Image(systemName: "camera.fill")
                                .font(.title2)
                                .foregroundColor(.white)
                                .background(Color.blue)
                                .clipShape(Circle())
                                .offset(x: 35, y: 35)
                        }
                    }
                )
            }
            .disabled(!isEditing)
            
            // Name and Email
            VStack(spacing: 4) {
                Text(user?.displayName ?? "Unknown User")
                    .font(.title2)
                    .fontWeight(.semibold)
                
                Text(user?.email ?? "")
                    .font(.subheadline)
                    .foregroundColor(.secondary)
            }
            
            // Sync Status
            HStack(spacing: 8) {
                Image(systemName: patient?.isSynced == true ? "checkmark.circle.fill" : "exclamationmark.circle.fill")
                    .foregroundColor(patient?.isSynced == true ? .green : .orange)
                
                Text(patient?.isSynced == true ? "Profile synced" : "Sync pending")
                    .font(.caption)
                    .foregroundColor(.secondary)
            }
        }
        .padding()
        .liquidGlassCardStyle()
    }
}

// MARK: - Patient Info Section

struct PatientInfoSection: View {
    let patient: PatientInfo
    let isEditing: Bool
    
    var body: some View {
        VStack(alignment: .leading, spacing: 16) {
            Text("Personal Information")
                .font(.headline)
                .padding(.horizontal)
            
            VStack(spacing: 12) {
                ProfileInfoRow(
                    title: "Blood Type",
                    value: patient.bloodType ?? "Not specified",
                    isEditing: isEditing
                )
                
                ProfileInfoRow(
                    title: "CNP",
                    value: patient.cnp ?? "Not specified",
                    isEditing: isEditing
                )
            }
            .padding()
            .liquidGlassCardStyle()
        }
    }
}

// MARK: - Health Metrics Section

struct HealthMetricsSection: View {
    let patient: PatientInfo
    let isEditing: Bool
    
    var body: some View {
        VStack(alignment: .leading, spacing: 16) {
            Text("Health Metrics")
                .font(.headline)
                .padding(.horizontal)
            
            LazyVGrid(columns: Array(repeating: GridItem(.flexible()), count: 2), spacing: 12) {
                HealthMetricCard(
                    title: "Weight",
                    value: patient.weight.map { String(format: "%.1f lbs", $0) } ?? "Not set",
                    icon: "scalemass.fill",
                    color: .brown,
                    isEditing: isEditing
                )
                
                HealthMetricCard(
                    title: "Height",
                    value: patient.height.map { String(format: "%.1f in", $0) } ?? "Not set",
                    icon: "ruler.fill",
                    color: .gray,
                    isEditing: isEditing
                )
                
                HealthMetricCard(
                    title: "Blood Pressure",
                    value: {
                        if let systolic = patient.bloodPressureSystolic,
                           let diastolic = patient.bloodPressureDiastolic {
                            return "\(systolic)/\(diastolic) mmHg"
                        }
                        return "Not set"
                    }(),
                    icon: "drop.fill",
                    color: .blue,
                    isEditing: isEditing
                )
                
                HealthMetricCard(
                    title: "Cholesterol",
                    value: patient.cholesterol.map { String(format: "%.1f mg/dL", $0) } ?? "Not set",
                    icon: "drop.triangle.fill",
                    color: .purple,
                    isEditing: isEditing
                )
            }
            .padding(.horizontal)
        }
    }
}

// MARK: - Health Metric Card

struct HealthMetricCard: View {
    let title: String
    let value: String
    let icon: String
    let color: Color
    let isEditing: Bool
    
    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            HStack {
                Image(systemName: icon)
                    .font(.title2)
                    .foregroundColor(color)
                
                Spacer()
                
                if isEditing {
                    Button("Edit") {
                        // Edit action
                    }
                    .font(.caption)
                    .liquidGlassButtonStyle()
                }
            }
            
            Text(title)
                .font(.caption)
                .foregroundColor(.secondary)
            
            Text(value)
                .font(.subheadline)
                .fontWeight(.semibold)
        }
        .padding()
        .liquidGlassCardStyle()
    }
}

// MARK: - Medical Info Section

struct MedicalInfoSection: View {
    let patient: PatientInfo
    let isEditing: Bool
    
    var body: some View {
        VStack(alignment: .leading, spacing: 16) {
            Text("Medical Information")
                .font(.headline)
                .padding(.horizontal)
            
            VStack(spacing: 12) {
                ProfileInfoRow(
                    title: "Allergies",
                    value: patient.allergies ?? "None specified",
                    isEditing: isEditing,
                    isMultiline: true
                )
                
                ProfileInfoRow(
                    title: "Medical History",
                    value: patient.medicalHistoryNotes ?? "No notes",
                    isEditing: isEditing,
                    isMultiline: true
                )
            }
            .padding()
            .liquidGlassCardStyle()
        }
    }
}

// MARK: - Profile Info Row

struct ProfileInfoRow: View {
    let title: String
    let value: String
    let isEditing: Bool
    let isMultiline: Bool
    
    init(title: String, value: String, isEditing: Bool, isMultiline: Bool = false) {
        self.title = title
        self.value = value
        self.isEditing = isEditing
        self.isMultiline = isMultiline
    }
    
    var body: some View {
        VStack(alignment: .leading, spacing: 4) {
            Text(title)
                .font(.caption)
                .foregroundColor(.secondary)
            
            if isEditing {
                if isMultiline {
                    TextEditor(text: .constant(value))
                        .frame(minHeight: 60)
                        .padding(8)
                        .background(Color(.systemGray6))
                        .cornerRadius(8)
                } else {
                    TextField(title, text: .constant(value))
                        .textFieldStyle(.roundedBorder)
                }
            } else {
                Text(value)
                    .font(.subheadline)
                    .foregroundColor(value.contains("Not") || value.contains("None") ? .secondary : .primary)
            }
        }
    }
}

// MARK: - Profile Actions Section

struct ProfileActionsSection: View {
    @EnvironmentObject var engineWrapper: MobileEngineWrapper
    
    var body: some View {
        VStack(alignment: .leading, spacing: 16) {
            Text("Actions")
                .font(.headline)
                .padding(.horizontal)
            
            VStack(spacing: 12) {
                ProfileActionButton(
                    title: "Sync Profile",
                    subtitle: "Upload changes to cloud",
                    icon: "icloud.and.arrow.up",
                    color: .blue
                ) {
                    Task { await engineWrapper.performSync() }
                }
                
                ProfileActionButton(
                    title: "Export Health Data",
                    subtitle: "Download your data",
                    icon: "square.and.arrow.up",
                    color: .green
                ) {
                    // Export action
                }
                
                ProfileActionButton(
                    title: "Privacy Settings",
                    subtitle: "Manage data sharing",
                    icon: "hand.raised.fill",
                    color: .orange
                ) {
                    // Privacy action
                }
            }
            .padding()
            .liquidGlassCardStyle()
        }
    }
}

// MARK: - Profile Action Button

struct ProfileActionButton: View {
    let title: String
    let subtitle: String
    let icon: String
    let color: Color
    let action: () -> Void
    
    var body: some View {
        Button(action: action) {
            HStack(spacing: 12) {
                Image(systemName: icon)
                    .font(.title2)
                    .foregroundColor(color)
                    .frame(width: 30)
                
                VStack(alignment: .leading, spacing: 2) {
                    Text(title)
                        .font(.subheadline)
                        .fontWeight(.medium)
                        .foregroundColor(.primary)
                    
                    Text(subtitle)
                        .font(.caption)
                        .foregroundColor(.secondary)
                }
                
                Spacer()
                
                Image(systemName: "chevron.right")
                    .font(.caption)
                    .foregroundColor(.secondary)
            }
        }
        .buttonStyle(.plain)
    }
}

struct ProfileView_Previews: PreviewProvider {
    static var previews: some View {
        ProfileView()
            .environmentObject(MobileEngineWrapper())
    }
}