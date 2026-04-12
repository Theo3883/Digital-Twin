import SwiftUI

struct PatientView: View {
    @EnvironmentObject private var container: AppContainer
    @StateObject private var profileVM: ProfileViewModel
    @StateObject private var medsVM: MedicationsViewModel
    @State private var selectedSection: PatientSection = .overview
    @State private var showSignOutAlert = false

    private let profileRepository: ProfileRepository
    private let ocrRepository: OcrRepository
    private let addSheetViewModel: AddMedicationSheetViewModel

    enum PatientSection: String, CaseIterable {
        case overview = "Overview"
        case medications = "Medications"
        case documents = "Documents"
    }

    init(
        profileVM: ProfileViewModel,
        medsVM: MedicationsViewModel,
        addSheetViewModel: AddMedicationSheetViewModel,
        profileRepository: ProfileRepository,
        ocrRepository: OcrRepository
    ) {
        _profileVM = StateObject(wrappedValue: profileVM)
        _medsVM = StateObject(wrappedValue: medsVM)
        self.addSheetViewModel = addSheetViewModel
        self.profileRepository = profileRepository
        self.ocrRepository = ocrRepository
    }

    private var completionPercentage: Double {
        guard let p = profileVM.patient else { return 0 }
        var filled = 0
        let total = 5
        if p.cnp != nil { filled += 1 }
        if p.bloodType != nil { filled += 1 }
        if p.weight != nil { filled += 1 }
        if p.height != nil { filled += 1 }
        if p.allergies != nil { filled += 1 }
        return Double(filled) / Double(total)
    }

    private var profileIdentitySubtitle: String {
        guard let patient = profileVM.patient else {
            return "Medical profile"
        }

        var parts: [String] = []

        if let bloodType = patient.bloodType {
            parts.append(bloodType)
        }

        if let cnp = patient.cnp {
            parts.append("CNP: \(cnp.prefix(4))****")
        }

        return parts.isEmpty ? "Medical profile" : parts.joined(separator: " • ")
    }

    var body: some View {
        NavigationView {
            ZStack(alignment: .bottom) {
                MeshGradientBackground()
                
                ScrollView {
                    VStack(spacing: 16) {
                        // Profile Header
                        profileHeader

                        // "Create Medical Profile" CTA
                        if profileVM.patient == nil {
                            NoPatientProfileProfileCard {
                                container.shouldPresentProfileEdit = true
                            }
                        }

                        // Segment Picker
                        sectionPicker

                        // Content based on selected section
                        switch selectedSection {
                        case .overview:
                            overviewContent
                        case .medications:
                            medicationsContent
                        case .documents:
                            documentsContent
                        }

                        Spacer(minLength: 100)
                    }
                    .padding(16)
                }
                .pageEnterAnimation()

                // Floating buttons for medications section
                if selectedSection == .medications {
                    floatingActionButtons
                }
            }
            .task {
                await profileVM.load()
                await medsVM.refresh()
            }
            .refreshable {
                await profileVM.load()
                await medsVM.refresh()
            }
            .alert("Sign Out?", isPresented: $showSignOutAlert) {
                Button("Sign Out", role: .destructive) {
                    profileVM.signOut()
                }
                Button("Cancel", role: .cancel) {}
            }
            .sheet(isPresented: $container.shouldPresentProfileEdit) {
                ProfileEditSheet(
                    viewModel: ProfileEditSheetViewModel(repository: profileRepository, patient: profileVM.patient)
                )
            }
            .onChange(of: container.shouldPresentProfileEdit) { _, newValue in
                if newValue == false {
                    Task { await profileVM.load() }
                }
            }
            .sheet(isPresented: $medsVM.isAddSheetPresented) {
                AddMedicationSheet(viewModel: addSheetViewModel)
            }
            .sheet(isPresented: $medsVM.isInteractionsSheetPresented) {
                InteractionsSheet(interactions: medsVM.interactions)
            }
            .alert("End Medication", isPresented: $medsVM.isEndReasonDialogPresented) {
                TextField("Reason (optional)", text: $medsVM.endReason)
                Button("End") {
                    medsVM.confirmEndMedication()
                }
                Button("Cancel", role: .cancel) { medsVM.endReason = "" }
            }
            .navigationBarHidden(true)
        }
    }

    // MARK: - Profile Header

    private var profileHeader: some View {
        VStack(spacing: 10) {
            HStack(alignment: .center, spacing: 12) {
                // Compact completion ring + avatar for Home-like proportions
                ZStack {
                    Circle()
                        .stroke(Color.white.opacity(0.1), lineWidth: 3)
                        .frame(width: 72, height: 72)

                    Circle()
                        .trim(from: 0, to: completionPercentage)
                        .stroke(LiquidGlass.tealPrimary, style: StrokeStyle(lineWidth: 3, lineCap: .round))
                        .frame(width: 72, height: 72)
                        .rotationEffect(.degrees(-90))
                        .animation(.easeInOut(duration: 0.8), value: completionPercentage)

                    AsyncImage(url: profileVM.currentUser?.photoUrl.flatMap(URL.init)) { image in
                        image
                            .resizable()
                            .aspectRatio(contentMode: .fill)
                    } placeholder: {
                        Image(systemName: "person.circle.fill")
                            .font(.system(size: 40))
                            .foregroundColor(.white.opacity(0.3))
                    }
                    .frame(width: 60, height: 60)
                    .clipShape(Circle())
                }

                VStack(alignment: .leading, spacing: 3) {
                    Text(profileVM.currentUser?.displayName ?? "Unknown User")
                        .font(.system(size: 18, weight: .semibold))
                        .foregroundColor(.white)
                        .lineLimit(1)

                    Text(profileIdentitySubtitle)
                        .font(.system(size: 12))
                        .foregroundColor(.white.opacity(0.6))
                        .lineLimit(1)
                }

                Spacer(minLength: 8)

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
                        .frame(width: 34, height: 34)
                }
            }

            // Quick stats row
            HStack(spacing: 12) {
                quickStatPill(
                    icon: "heart.fill",
                    value: "\(completionPercentage >= 1 ? "✓" : "\(Int(completionPercentage * 100))%")",
                    label: "Profile",
                    color: completionPercentage >= 1 ? LiquidGlass.greenPositive : LiquidGlass.amberWarning
                )
                quickStatPill(
                    icon: "pills.fill",
                    value: "\(medsVM.activeMedications.count)",
                    label: "Active Meds",
                    color: LiquidGlass.tealPrimary
                )
                quickStatPill(
                    icon: "doc.text.fill",
                    value: "\(profileVM.ocrDocuments.count)",
                    label: "Documents",
                    color: LiquidGlass.bluePrimary
                )
            }
            .frame(maxWidth: .infinity)
        }
        .frame(maxWidth: .infinity)
        .glassCard()
    }

    private func quickStatPill(icon: String, value: String, label: String, color: Color) -> some View {
        VStack(spacing: 3) {
            Image(systemName: icon)
                .font(.system(size: 11))
                .foregroundColor(color)
            Text(value)
                .font(.system(size: 14, weight: .bold, design: .rounded))
                .foregroundColor(.white)
            Text(label)
                .font(.system(size: 8))
                .foregroundColor(.white.opacity(0.4))
        }
        .frame(maxWidth: .infinity)
    }

    // MARK: - Section Picker

    private var sectionPicker: some View {
        HStack(spacing: 4) {
            ForEach(PatientSection.allCases, id: \.self) { section in
                Button {
                    withAnimation(.easeInOut(duration: 0.25)) {
                        selectedSection = section
                    }
                } label: {
                    Text(section.rawValue)
                        .font(.system(size: 13, weight: .medium))
                        .foregroundColor(selectedSection == section ? .white : .white.opacity(0.45))
                        .frame(maxWidth: .infinity)
                        .padding(.vertical, 10)
                        .background {
                            if selectedSection == section {
                                RoundedRectangle(cornerRadius: LiquidGlass.radiusButton)
                                    .fill(LiquidGlass.tealPrimary.opacity(0.25))
                            }
                        }
                }
            }
        }
        .padding(4)
        .background {
            RoundedRectangle(cornerRadius: LiquidGlass.radiusButton + 4)
                .fill(.white.opacity(0.06))
        }
    }

    // MARK: - Overview Content

    @ViewBuilder
    private var overviewContent: some View {
        // Completion Checklist
        if profileVM.patient != nil && completionPercentage < 1.0 {
            completionChecklist
        }

        // Vital Stats Grid
        if let patient = profileVM.patient {
            vitalStatsGrid(patient: patient)
        }

        // Medical History Timeline
        if !profileVM.medicalHistory.isEmpty {
            medicalHistoryTimeline
        }

        // Assigned Doctors
        DoctorAssignmentCard(doctors: profileVM.assignedDoctors)
    }

    // MARK: - Medications Content

    @ViewBuilder
    private var medicationsContent: some View {
        // Interaction Banner
        interactionBanner

        // Interaction Detail Cards
        if !medsVM.interactions.isEmpty {
            interactionDetailCards
        }

        // Active Medications
        if !medsVM.activeMedications.isEmpty {
            activeMedicationsSection
        }

        // Ended Medications
        if !medsVM.inactiveMedications.isEmpty {
            endedMedicationsSection
        }

        if medsVM.medications.isEmpty {
            EmptyMedicationsView()
        }
    }

    // MARK: - Documents Content

    @ViewBuilder
    private var documentsContent: some View {
        documentsCard

        // Show recent documents list
        if !profileVM.ocrDocuments.isEmpty {
            VStack(alignment: .leading, spacing: 12) {
                Text("Documentation")
                    .font(.subheadline.weight(.medium))
                    .foregroundColor(.white)
                    .padding(.horizontal, 4)

                ForEach(Array(profileVM.ocrDocuments.prefix(10))) { doc in
                    NavigationLink {
                        MedicalDocumentDetailView(document: doc, repository: ocrRepository)
                    } label: {
                        HStack(spacing: 12) {
                            Image(systemName: doc.typeIcon)
                                .font(.system(size: 20))
                                .foregroundColor(LiquidGlass.tealPrimary)
                                .frame(width: 36, height: 36)

                            VStack(alignment: .leading, spacing: 2) {
                                Text(doc.displayType)
                                    .font(.system(size: 14, weight: .medium))
                                    .foregroundColor(.white)
                                    .lineLimit(1)
                                if let date = doc.createdAt {
                                    Text(date.formatted(date: .abbreviated, time: .shortened))
                                        .font(.caption2)
                                        .foregroundColor(.white.opacity(0.4))
                                }
                            }

                            Spacer()

                            Image(systemName: "chevron.right")
                                .font(.caption)
                                .foregroundColor(.white.opacity(0.2))
                        }
                        .glassCard()
                    }
                    .buttonStyle(.plain)
                }
            }
        } else {
            VStack(spacing: 16) {
                Image(systemName: "doc.text.magnifyingglass")
                    .font(.system(size: 50))
                    .foregroundColor(.white.opacity(0.3))
                Text("No Documents")
                    .font(.title3.weight(.semibold))
                    .foregroundColor(.white)
                Text("Import medical documents to keep your records organized.")
                    .font(.subheadline)
                    .foregroundColor(.white.opacity(0.65))
                    .multilineTextAlignment(.center)
            }
            .frame(maxWidth: .infinity)
            .padding(.vertical, 60)
        }
    }

    // MARK: - Completion Checklist

    private var completionChecklist: some View {
        let patient = profileVM.patient
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

    // MARK: - Vital Stats Grid

    private func vitalStatsGrid(patient: PatientInfo) -> some View {
        LazyVGrid(columns: [GridItem(.flexible()), GridItem(.flexible())], spacing: 12) {
            VitalStatTile(label: "Weight", value: patient.weight.map { String(format: "%.0f", $0) } ?? "--", unit: "lbs", icon: "scalemass.fill", color: .brown)
            VitalStatTile(label: "Height", value: patient.height.map { String(format: "%.0f", $0) } ?? "--", unit: "in", icon: "ruler.fill", color: .gray)
            VitalStatTile(label: "BMI", value: bmi(patient: patient), unit: bmiCategory(patient: patient), icon: "figure.stand", color: bmiColor(patient: patient))
            VitalStatTile(label: "Resting HR", value: profileVM.latestHeartRate.map { "\($0)" } ?? "--", unit: "bpm", icon: "heart.fill", color: LiquidGlass.redCritical)
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
        let bmiVal = (w / (h * h)) * 703
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

            ForEach(profileVM.medicalHistory.prefix(5)) { entry in
                HStack(alignment: .top, spacing: 12) {
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
                if !profileVM.ocrDocuments.isEmpty {
                    Text("\(profileVM.ocrDocuments.count)")
                        .font(.caption2.weight(.bold))
                        .foregroundColor(.white)
                        .padding(.horizontal, 8)
                        .padding(.vertical, 3)
                        .background(RoundedRectangle(cornerRadius: LiquidGlass.radiusChip).fill(LiquidGlass.tealPrimary.opacity(0.3)))
                }
            }

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
        }
        .glassCard()
    }

    // MARK: - Interaction Banner

    private var interactionBanner: some View {
        Group {
            if medsVM.interactions.isEmpty && !medsVM.activeMedications.isEmpty {
                HStack(spacing: 8) {
                    Image(systemName: "checkmark.shield.fill")
                        .foregroundColor(LiquidGlass.greenPositive)
                    Text("✓ No Interactions Found")
                        .font(.subheadline.weight(.medium))
                        .foregroundColor(.white)
                }
                .frame(maxWidth: .infinity)
                .glassBanner(tint: LiquidGlass.greenPositive.opacity(0.15))
            } else if !medsVM.interactions.isEmpty {
                let highCount = medsVM.interactions.filter { $0.severity == 2 }.count
                let medCount = medsVM.interactions.filter { $0.severity == 1 }.count
                let lowCount = medsVM.interactions.filter { $0.severity == 0 }.count

                VStack(alignment: .leading, spacing: 6) {
                    HStack(spacing: 8) {
                        Image(systemName: "exclamationmark.triangle.fill")
                            .foregroundColor(.white)
                        Text("⚠ \(medsVM.interactions.count) Interaction\(medsVM.interactions.count == 1 ? "" : "s") Detected")
                            .font(.subheadline.weight(.semibold))
                            .foregroundColor(.white)
                    }
                    HStack(spacing: 8) {
                        if highCount > 0 {
                            Text("\(highCount) High")
                                .font(.caption2.weight(.medium))
                                .glassChip(tint: LiquidGlass.redCritical)
                        }
                        if medCount > 0 {
                            Text("\(medCount) Medium")
                                .font(.caption2.weight(.medium))
                                .glassChip(tint: LiquidGlass.amberWarning)
                        }
                        if lowCount > 0 {
                            Text("\(lowCount) Low")
                                .font(.caption2.weight(.medium))
                                .glassChip(tint: LiquidGlass.greenPositive)
                        }
                    }
                }
                .frame(maxWidth: .infinity, alignment: .leading)
                .glassBanner(tint: highCount > 0 ? LiquidGlass.redCritical.opacity(0.3) : LiquidGlass.amberWarning.opacity(0.3))
            } else if medsVM.medications.isEmpty {
                HStack(spacing: 8) {
                    Image(systemName: "pills.fill")
                        .foregroundColor(.white.opacity(0.5))
                    Text("No Medications")
                        .font(.subheadline)
                        .foregroundColor(.white.opacity(0.65))
                }
                .frame(maxWidth: .infinity)
                .glassBanner(tint: LiquidGlass.greenPositive.opacity(0.1))
            }
        }
    }

    // MARK: - Interaction Detail Cards

    private var interactionDetailCards: some View {
        VStack(spacing: 8) {
            ForEach(medsVM.interactions) { interaction in
                HStack(alignment: .top, spacing: 12) {
                    Image(systemName: "shield.fill")
                        .foregroundColor(interaction.severity == 2 ? LiquidGlass.redCritical : LiquidGlass.amberWarning)
                        .font(.title3)

                    VStack(alignment: .leading, spacing: 6) {
                        HStack {
                            Text("\(interaction.drugARxCui) + \(interaction.drugBRxCui)")
                                .font(.subheadline.weight(.medium))
                                .foregroundColor(.white)
                            Spacer()
                            MedicationSafetyBadge(severity: interaction.severity)
                        }
                        Text(interaction.description)
                            .font(.caption)
                            .foregroundColor(.white.opacity(0.65))
                            .lineLimit(3)
                    }
                }
                .padding()
                .overlay(alignment: .leading) {
                    Rectangle()
                        .fill(interaction.severity == 2 ? LiquidGlass.redCritical : LiquidGlass.amberWarning)
                        .frame(width: 4)
                }
                .glassEffect(.regular, in: RoundedRectangle(cornerRadius: LiquidGlass.radiusCard))
            }
        }
    }

    // MARK: - Active Medications Section

    private var activeMedicationsSection: some View {
        VStack(alignment: .leading, spacing: 12) {
            Text("Active Medications · \(medsVM.activeMedications.count) drug\(medsVM.activeMedications.count == 1 ? "" : "s")")
                .font(.subheadline.weight(.medium))
                .foregroundColor(.white.opacity(0.65))
                .padding(.horizontal, 4)

            ForEach(medsVM.activeMedications) { med in
                MedicationCard(medication: med)
                    .contextMenu {
                        Button {
                            medsVM.promptEndMedication(med)
                        } label: {
                            Label("End Medication", systemImage: "stop.circle")
                        }
                        Button(role: .destructive) {
                            medsVM.removeMedication(med)
                        } label: {
                            Label("Remove", systemImage: "trash")
                        }
                    }
            }
        }
    }

    // MARK: - Ended Medications Section

    private var endedMedicationsSection: some View {
        VStack(alignment: .leading, spacing: 12) {
            Text("Ended · \(medsVM.inactiveMedications.count) medication\(medsVM.inactiveMedications.count == 1 ? "" : "s")")
                .font(.caption.weight(.medium))
                .foregroundColor(.white.opacity(0.4))
                .padding(.horizontal, 4)

            ForEach(medsVM.inactiveMedications) { med in
                MedicationCard(medication: med)
                    .opacity(0.75)
            }
        }
    }

    // MARK: - FABs

    private var floatingActionButtons: some View {
        HStack(spacing: 12) {
            Button(action: { medsVM.isInteractionsSheetPresented = true }) {
                HStack(spacing: 6) {
                    Image(systemName: "magnifyingglass")
                        .font(.system(size: 14))
                    Text("Check Interactions")
                        .font(.caption.weight(.medium))
                }
                .foregroundColor(.white)
            }
            .glassPill(tint: LiquidGlass.tealPrimary.opacity(0.15))
            .disabled(medsVM.interactions.isEmpty)

            Button(action: { medsVM.isAddSheetPresented = true }) {
                HStack(spacing: 6) {
                    Image(systemName: "plus")
                        .font(.system(size: 14))
                    Text("Add Medication")
                        .font(.caption.weight(.medium))
                }
                .foregroundColor(.white)
            }
            .glassPill(tint: LiquidGlass.tealPrimary.opacity(0.2))
        }
        .padding(.bottom, 16)
    }
}
