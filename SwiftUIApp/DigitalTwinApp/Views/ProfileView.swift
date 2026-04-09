import SwiftUI

struct ProfileView: View {
    @EnvironmentObject var engineWrapper: MobileEngineWrapper
    @State private var showEditSheet = false
    @State private var showSignOutAlert = false

    private var completionPercentage: Double {
        guard let p = engineWrapper.patientProfile else { return 0 }
        var filled = 0
        let total = 7
        if p.bloodType != nil { filled += 1 }
        if p.weight != nil { filled += 1 }
        if p.height != nil { filled += 1 }
        if p.allergies != nil { filled += 1 }
        if p.bloodPressureSystolic != nil { filled += 1 }
        if p.cholesterol != nil { filled += 1 }
        if p.cnp != nil { filled += 1 }
        return Double(filled) / Double(total)
    }

    var body: some View {
        NavigationView {
            ScrollView {
            VStack(spacing: 16) {
                // Profile Header with completion ring + menu
                profileHeader

                // "Create Medical Profile" CTA when no profile exists
                if engineWrapper.patientProfile == nil {
                    createProfileCTA
                }

                // Completion Checklist (if < 100%)
                if engineWrapper.patientProfile != nil && completionPercentage < 1.0 {
                    completionChecklist
                }

                // Vital Stats Grid 2×3
                if let patient = engineWrapper.patientProfile {
                    vitalStatsGrid(patient: patient)
                }

                // Medical History Timeline
                if !engineWrapper.medicalHistory.isEmpty {
                    medicalHistoryTimeline
                }

                // Medical Documents Card
                documentsCard

                Spacer(minLength: 100)
            }
            .padding(16)
        }
        .pageEnterAnimation()
        .task {
            await engineWrapper.getCurrentUser()
            await engineWrapper.loadPatientProfile()
            await engineWrapper.loadMedicalHistory()
        }
        .alert("Sign Out?", isPresented: $showSignOutAlert) {
            Button("Sign Out", role: .destructive) {
                Task { await engineWrapper.signOut() }
            }
            Button("Cancel", role: .cancel) {}
        }
        .sheet(isPresented: $showEditSheet) {
            ProfileEditSheet()
        }
        .navigationBarHidden(true)
        } // NavigationView
    }

    // MARK: - Profile Header

    private var profileHeader: some View {
        ZStack(alignment: .topTrailing) {
            VStack(spacing: 12) {
                // Completion ring + avatar
                ZStack {
                    // Ring
                    Circle()
                        .stroke(Color.white.opacity(0.1), lineWidth: 4)
                        .frame(width: 96, height: 96)
                    Circle()
                        .trim(from: 0, to: completionPercentage)
                        .stroke(LiquidGlass.tealPrimary, style: StrokeStyle(lineWidth: 4, lineCap: .round))
                        .frame(width: 96, height: 96)
                        .rotationEffect(.degrees(-90))
                        .animation(.easeInOut(duration: 0.8), value: completionPercentage)

                    // Avatar
                    AsyncImage(url: engineWrapper.currentUser?.photoUrl.flatMap(URL.init)) { image in
                        image
                            .resizable()
                            .aspectRatio(contentMode: .fill)
                    } placeholder: {
                        Image(systemName: "person.circle.fill")
                            .font(.system(size: 56))
                            .foregroundColor(.white.opacity(0.3))
                    }
                    .frame(width: 80, height: 80)
                    .clipShape(Circle())
                }

                // Name
                Text(engineWrapper.currentUser?.displayName ?? "Unknown User")
                    .font(.title3.weight(.semibold))
                    .foregroundColor(.white)

                // Subtitle
                HStack(spacing: 8) {
                    if let patient = engineWrapper.patientProfile {
                        if let bloodType = patient.bloodType {
                            Text(bloodType)
                                .font(.caption)
                                .foregroundColor(LiquidGlass.tealPrimary)
                        }
                        if let cnp = patient.cnp {
                            Text("CNP: \(cnp.prefix(4))****")
                                .font(.caption)
                                .foregroundColor(.white.opacity(0.4))
                        }
                    }
                }

                // Completion percentage
                Text("\(Int(completionPercentage * 100))% Complete")
                    .font(.caption2.weight(.medium))
                    .foregroundColor(completionPercentage >= 1 ? LiquidGlass.greenPositive : LiquidGlass.amberWarning)
            }
            .frame(maxWidth: .infinity)
            .glassCard()

            // 3-dot menu
            Menu {
                Button { showEditSheet = true } label: {
                    Label("Edit Profile", systemImage: "pencil")
                }
                Button(role: .destructive) { showSignOutAlert = true } label: {
                    Label("Sign Out", systemImage: "rectangle.portrait.and.arrow.right")
                }
            } label: {
                Image(systemName: "ellipsis")
                    .font(.system(size: 16, weight: .medium))
                    .foregroundColor(.white.opacity(0.6))
                    .frame(width: 36, height: 36)
            }
            .padding(12)
        }
    }

    // MARK: - Create Profile CTA

    private var createProfileCTA: some View {
        VStack(spacing: 14) {
            Image(systemName: "person.crop.circle.badge.plus")
                .font(.system(size: 36))
                .foregroundColor(LiquidGlass.amberWarning)

            Text("No medical profile yet")
                .font(.subheadline.weight(.semibold))
                .foregroundColor(.white)

            Text("Create your medical profile to unlock personalized health insights and vitals tracking.")
                .font(.caption)
                .foregroundColor(.white.opacity(0.5))
                .multilineTextAlignment(.center)

            Button {
                showEditSheet = true
            } label: {
                HStack(spacing: 6) {
                    Image(systemName: "plus.circle.fill")
                    Text("Create Medical Profile")
                        .font(.subheadline.weight(.semibold))
                }
                .foregroundColor(.white)
                .frame(maxWidth: .infinity)
                .padding(.vertical, 12)
                .background {
                    RoundedRectangle(cornerRadius: LiquidGlass.radiusCard)
                        .fill(LiquidGlass.tealPrimary.opacity(0.3))
                }
            }
        }
        .glassCard(tint: LiquidGlass.amberWarning.opacity(0.08))
    }

    // MARK: - Completion Checklist

    private var completionChecklist: some View {
        let patient = engineWrapper.patientProfile
        let items: [(String, Bool)] = [
            ("Personal info", patient?.cnp != nil),
            ("Blood type", patient?.bloodType != nil),
            ("Weight & Height", patient?.weight != nil && patient?.height != nil),
            ("Blood pressure", patient?.bloodPressureSystolic != nil),
            ("Allergies", patient?.allergies != nil),
        ]

        return VStack(alignment: .leading, spacing: 10) {
            Text("Complete Your Profile")
                .font(.subheadline.weight(.medium))
                .foregroundColor(.white)

            ForEach(items, id: \.0) { label, done in
                HStack(spacing: 10) {
                    Image(systemName: done ? "checkmark.circle.fill" : "circle")
                        .font(.system(size: 16))
                        .foregroundColor(done ? LiquidGlass.greenPositive : .white.opacity(0.3))
                    Text(label)
                        .font(.caption)
                        .foregroundColor(done ? .white.opacity(0.5) : .white)
                    Spacer()
                }
            }
        }
        .glassCard()
    }

    // MARK: - Vital Stats Grid 2×3

    private func vitalStatsGrid(patient: PatientInfo) -> some View {
        LazyVGrid(columns: [GridItem(.flexible()), GridItem(.flexible())], spacing: 12) {
            VitalStatTile(label: "Weight", value: patient.weight.map { String(format: "%.0f", $0) } ?? "--", unit: "lbs", icon: "scalemass.fill", color: .brown)
            VitalStatTile(label: "Height", value: patient.height.map { String(format: "%.0f", $0) } ?? "--", unit: "in", icon: "ruler.fill", color: .gray)
            VitalStatTile(label: "BMI", value: bmi(patient: patient), unit: bmiCategory(patient: patient), icon: "figure.stand", color: bmiColor(patient: patient))
            VitalStatTile(label: "Resting HR", value: engineWrapper.latestVitals?.heartRate.map { "\($0)" } ?? "--", unit: "bpm", icon: "heart.fill", color: LiquidGlass.redCritical)
            VitalStatTile(label: "Blood Pressure", value: {
                if let s = patient.bloodPressureSystolic, let d = patient.bloodPressureDiastolic {
                    return "\(s)/\(d)"
                }
                return "--"
            }(), unit: "mmHg", icon: "drop.fill", color: .blue)
            VitalStatTile(label: "Cholesterol", value: patient.cholesterol.map { String(format: "%.0f", $0) } ?? "--", unit: "mg/dL", icon: "drop.triangle.fill", color: .purple)
        }
    }

    private func bmi(patient: PatientInfo) -> String {
        guard let w = patient.weight, let h = patient.height, h > 0 else { return "--" }
        let bmiVal = (w / (h * h)) * 703 // lbs/in² → BMI
        return String(format: "%.1f", bmiVal)
    }

    private func bmiValue(patient: PatientInfo) -> Double? {
        guard let w = patient.weight, let h = patient.height, h > 0 else { return nil }
        return (w / (h * h)) * 703
    }

    private func bmiCategory(patient: PatientInfo) -> String {
        guard let val = bmiValue(patient: patient) else { return "" }
        if val < 18.5 { return "Underweight" }
        if val < 25 { return "Normal" }
        if val < 30 { return "Overweight" }
        return "Obese"
    }

    private func bmiColor(patient: PatientInfo) -> Color {
        guard let val = bmiValue(patient: patient) else { return LiquidGlass.tealPrimary }
        if val < 18.5 { return LiquidGlass.amberWarning }
        if val < 25 { return LiquidGlass.greenPositive }
        if val < 30 { return LiquidGlass.amberWarning }
        return LiquidGlass.redCritical
    }

    // MARK: - Medical History Timeline

    private var medicalHistoryTimeline: some View {
        VStack(alignment: .leading, spacing: 0) {
            Text("Medical History")
                .font(.subheadline.weight(.medium))
                .foregroundColor(.white)
                .padding(.bottom, 12)

            ForEach(engineWrapper.medicalHistory.prefix(5)) { entry in
                HStack(alignment: .top, spacing: 12) {
                    // Timeline indicator
                    VStack(spacing: 0) {
                        Circle()
                            .fill(LiquidGlass.tealPrimary)
                            .frame(width: 12, height: 12)
                            .shadow(color: LiquidGlass.tealPrimary.opacity(0.5), radius: 4)
                        Rectangle()
                            .fill(.white.opacity(0.1))
                            .frame(width: 2)
                    }
                    .frame(width: 12)

                    // Content
                    VStack(alignment: .leading, spacing: 4) {
                        Text(entry.displayTitle)
                            .font(.system(size: 14, weight: .medium))
                            .foregroundColor(.white)
                        if let summary = entry.summary ?? entry.notes {
                            Text(summary)
                                .font(.caption)
                                .foregroundColor(.white.opacity(0.5))
                                .lineLimit(2)
                        }
                    }
                    .padding(.bottom, 16)
                }
            }
        }
        .frame(maxHeight: 270)
        .glassCard()
    }

    // MARK: - Documents Card

    private var documentsCard: some View {
        VStack(alignment: .leading, spacing: 14) {
            HStack(spacing: 10) {
                Image(systemName: "doc.text.fill")
                    .font(.title3)
                    .foregroundColor(LiquidGlass.tealPrimary)
                Text("Medical Documents")
                    .font(.subheadline.weight(.medium))
                    .foregroundColor(.white)
                Spacer()
                if !engineWrapper.ocrDocuments.isEmpty {
                    Text("\(engineWrapper.ocrDocuments.count)")
                        .font(.caption2.weight(.bold))
                        .foregroundColor(.white)
                        .padding(.horizontal, 8)
                        .padding(.vertical, 3)
                        .background(RoundedRectangle(cornerRadius: LiquidGlass.radiusChip).fill(LiquidGlass.tealPrimary.opacity(0.3)))
                }
            }

            HStack(spacing: 12) {
                NavigationLink(destination: OcrDocumentView()) {
                    Text("Import Documents")
                        .font(.caption.weight(.medium))
                        .foregroundColor(.white)
                        .frame(maxWidth: .infinity)
                        .padding(.vertical, 10)
                        .background {
                            RoundedRectangle(cornerRadius: LiquidGlass.radiusButton)
                                .fill(LiquidGlass.tealPrimary.opacity(0.2))
                        }
                }

                NavigationLink(destination: OcrDocumentView()) {
                    Text("View Documents")
                        .font(.caption.weight(.medium))
                        .foregroundColor(.white.opacity(0.6))
                        .frame(maxWidth: .infinity)
                        .padding(.vertical, 10)
                        .background {
                            RoundedRectangle(cornerRadius: LiquidGlass.radiusButton)
                                .fill(.white.opacity(0.05))
                        }
                }
            }
        }
        .glassCard()
    }
}

// MARK: - Vital Stat Tile

struct VitalStatTile: View {
    let label: String
    let value: String
    let unit: String
    let icon: String
    let color: Color

    var body: some View {
        VStack(spacing: 6) {
            Image(systemName: icon)
                .font(.system(size: 16))
                .foregroundColor(color)
            Text(value)
                .font(.system(size: 22, weight: .semibold, design: .rounded))
                .foregroundColor(.white)
            if !unit.isEmpty {
                Text(unit)
                    .font(.system(size: 10))
                    .foregroundColor(.white.opacity(0.35))
            }
            Text(label)
                .font(.system(size: 11))
                .foregroundColor(.white.opacity(0.5))
        }
        .frame(maxWidth: .infinity)
        .padding(.vertical, 16)
        .glassEffect(.regular, in: RoundedRectangle(cornerRadius: LiquidGlass.radiusInner))
    }
}

// MARK: - Profile Edit Sheet

struct ProfileEditSheet: View {
    @EnvironmentObject var engineWrapper: MobileEngineWrapper
    @Environment(\.dismiss) private var dismiss
    @State private var bloodType = ""
    @State private var allergies = ""
    @State private var weight = ""
    @State private var height = ""
    @State private var systolic = ""
    @State private var diastolic = ""
    @State private var cholesterol = ""
    @State private var cnp = ""
    @State private var isSaving = false

    var body: some View {
        NavigationView {
            Form {
                Section("Personal") {
                    TextField("Blood Type (e.g. A+)", text: $bloodType)
                    TextField("CNP", text: $cnp)
                }
                Section("Measurements") {
                    TextField("Weight (lbs)", text: $weight).keyboardType(.decimalPad)
                    TextField("Height (in)", text: $height).keyboardType(.decimalPad)
                }
                Section("Vitals") {
                    HStack {
                        TextField("Systolic", text: $systolic).keyboardType(.numberPad)
                        Text("/")
                        TextField("Diastolic", text: $diastolic).keyboardType(.numberPad)
                    }
                    TextField("Cholesterol (mg/dL)", text: $cholesterol).keyboardType(.decimalPad)
                }
                Section("Other") {
                    TextField("Allergies", text: $allergies)
                }
            }
            .navigationTitle("Edit Profile")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .cancellationAction) { Button("Cancel") { dismiss() } }
                ToolbarItem(placement: .confirmationAction) {
                    Button("Save") {
                        Task { await save() }
                    }
                    .disabled(isSaving)
                    .liquidGlassButtonStyle()
                }
            }
            .onAppear { populateFields() }
        }
        .presentationDetents([.large])
        .presentationDragIndicator(.visible)
    }

    private func populateFields() {
        guard let p = engineWrapper.patientProfile else { return }
        bloodType = p.bloodType ?? ""
        cnp = p.cnp ?? ""
        weight = p.weight.map { String(format: "%.1f", $0) } ?? ""
        height = p.height.map { String(format: "%.1f", $0) } ?? ""
        systolic = p.bloodPressureSystolic.map { "\($0)" } ?? ""
        diastolic = p.bloodPressureDiastolic.map { "\($0)" } ?? ""
        cholesterol = p.cholesterol.map { String(format: "%.1f", $0) } ?? ""
        allergies = p.allergies ?? ""
    }

    private func save() async {
        isSaving = true
        let input = PatientUpdateInfo(
            bloodType: bloodType.isEmpty ? nil : bloodType,
            allergies: allergies.isEmpty ? nil : allergies,
            medicalHistoryNotes: nil,
            weight: Double(weight),
            height: Double(height),
            bloodPressureSystolic: Int(systolic),
            bloodPressureDiastolic: Int(diastolic),
            cholesterol: Double(cholesterol),
            cnp: cnp.isEmpty ? nil : cnp
        )
        await engineWrapper.updatePatientProfile(input)
        dismiss()
    }
}