import SwiftUI

struct ProfileView: View {
    @EnvironmentObject private var container: AppContainer
    @StateObject private var viewModel: ProfileViewModel
    @State private var showSignOutAlert = false

    private let repository: ProfileRepository
    private let ocrRepository: OcrRepository

    init(viewModel: ProfileViewModel, repository: ProfileRepository, ocrRepository: OcrRepository) {
        _viewModel = StateObject(wrappedValue: viewModel)
        self.repository = repository
        self.ocrRepository = ocrRepository
    }

    private var completionPercentage: Double {
        guard let p = viewModel.patient else { return 0 }
        var filled = 0
        let total = 5
        if p.cnp != nil { filled += 1 }
        if p.bloodType != nil { filled += 1 }
        if p.weight != nil { filled += 1 }
        if p.height != nil { filled += 1 }
        if p.allergies != nil { filled += 1 }
        return Double(filled) / Double(total)
    }

    var body: some View {
        NavigationView {
            ScrollView {
            VStack(spacing: 16) {
                // Profile Header with completion ring + menu
                profileHeader

                // "Create Medical Profile" CTA when no profile exists
                if viewModel.patient == nil {
                    NoPatientProfileProfileCard {
                        container.shouldPresentProfileEdit = true
                    }
                }

                // Completion Checklist (if < 100%)
                if viewModel.patient != nil && completionPercentage < 1.0 {
                    completionChecklist
                }

                // Vital Stats Grid 2×3
                if let patient = viewModel.patient {
                    vitalStatsGrid(patient: patient)
                }

                // Medical History Timeline
                if !viewModel.medicalHistory.isEmpty {
                    medicalHistoryTimeline
                }

                // Assigned Doctors
                DoctorAssignmentCard(doctors: viewModel.assignedDoctors)

                // Medical Documents Card
                documentsCard

                Spacer(minLength: 100)
            }
            .padding(16)
        }
        .pageEnterAnimation()
        .task {
            await viewModel.load()
        }
        .alert("Sign Out?", isPresented: $showSignOutAlert) {
            Button("Sign Out", role: .destructive) {
                viewModel.signOut()
            }
            Button("Cancel", role: .cancel) {}
        }
        .sheet(isPresented: $container.shouldPresentProfileEdit) {
            ProfileEditSheet(
                viewModel: ProfileEditSheetViewModel(repository: repository, patient: viewModel.patient)
            )
        }
        .onChange(of: container.shouldPresentProfileEdit) { _, newValue in
            if newValue == false {
                Task { await viewModel.load() }
            }
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
                    AsyncImage(url: viewModel.currentUser?.photoUrl.flatMap(URL.init)) { image in
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
                Text(viewModel.currentUser?.displayName ?? "Unknown User")
                    .font(.title3.weight(.semibold))
                    .foregroundColor(.white)

                // Subtitle
                HStack(spacing: 8) {
                    if let patient = viewModel.patient {
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
                Button { container.shouldPresentProfileEdit = true } label: {
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

    // MARK: - Completion Checklist

    private var completionChecklist: some View {
        let patient = viewModel.patient
        let items: [(String, Bool)] = [
            ("Personal info", patient?.cnp != nil),
            ("Blood type", patient?.bloodType != nil),
            ("Weight", patient?.weight != nil),
            ("Height", patient?.height != nil),
            ("Allergies", patient?.allergies != nil),
        ]

        return VStack(alignment: .leading, spacing: 10) {
            Text("Complete Your Patient Profile")
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
            VitalStatTile(label: "Resting HR", value: viewModel.latestHeartRate.map { "\($0)" } ?? "--", unit: "bpm", icon: "heart.fill", color: LiquidGlass.redCritical)
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

            ForEach(viewModel.medicalHistory.prefix(5)) { entry in
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
                        if !entry.summary.isEmpty || !entry.notes.isEmpty {
                            Text(entry.summary.isEmpty ? entry.notes : entry.summary)
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
                if !viewModel.ocrDocuments.isEmpty {
                    Text("\(viewModel.ocrDocuments.count)")
                        .font(.caption2.weight(.bold))
                        .foregroundColor(.white)
                        .padding(.horizontal, 8)
                        .padding(.vertical, 3)
                        .background(RoundedRectangle(cornerRadius: LiquidGlass.radiusChip).fill(LiquidGlass.tealPrimary.opacity(0.3)))
                }
            }

            HStack(spacing: 12) {
                NavigationLink(
                    destination: OcrDocumentRootView(
                        repository: ocrRepository
                    )
                ) {
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

                NavigationLink(
                    destination: OcrDocumentRootView(
                        repository: ocrRepository
                    )
                ) {
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