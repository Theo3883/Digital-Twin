import SwiftUI

struct MedicationsView: View {
    @EnvironmentObject var engineWrapper: MobileEngineWrapper
    @State private var showingAddSheet = false
    @State private var showingInteractions = false
    @State private var searchText = ""
    @State private var drugSearchResults: [DrugSearchResult] = []
    @State private var interactions: [MedicationInteractionInfo] = []
    @State private var selectedMedication: MedicationInfo?
    @State private var showEndReasonDialog = false
    @State private var endReason = ""

    private var activeMedications: [MedicationInfo] {
        engineWrapper.medications.filter { $0.isActive }
    }

    private var inactiveMedications: [MedicationInfo] {
        engineWrapper.medications.filter { !$0.isActive }
    }

    var body: some View {
        ZStack(alignment: .bottom) {
            ScrollView {
                VStack(spacing: 16) {
                    // Interaction Banner
                    interactionBanner
                    
                    // Interaction Detail Cards
                    if !interactions.isEmpty {
                        interactionDetailCards
                    }
                    
                    // Active Medications
                    if !activeMedications.isEmpty {
                        activeMedicationsSection
                    }
                    
                    // Ended Medications
                    if !inactiveMedications.isEmpty {
                        endedMedicationsSection
                    }
                    
                    if engineWrapper.medications.isEmpty {
                        EmptyMedicationsView()
                    }
                    
                    Spacer(minLength: 80)
                }
                .padding(16)
            }
            .pageEnterAnimation()
            .task {
                await engineWrapper.loadMedications()
                await checkAllInteractions()
            }
            .refreshable {
                await engineWrapper.loadMedications()
                await checkAllInteractions()
            }
            
            // Floating Action Buttons
            floatingActionButtons
        }
        .sheet(isPresented: $showingAddSheet) {
            AddMedicationSheet(drugSearchResults: $drugSearchResults, searchText: $searchText)
        }
        .sheet(isPresented: $showingInteractions) {
            InteractionsSheet(interactions: interactions)
        }
        .alert("End Medication", isPresented: $showEndReasonDialog) {
            TextField("Reason (optional)", text: $endReason)
            Button("End") {
                if let med = selectedMedication {
                    Task {
                        let _ = await engineWrapper.discontinueMedication(id: med.id, reason: endReason.isEmpty ? nil : endReason)
                        endReason = ""
                    }
                }
            }
            Button("Cancel", role: .cancel) { endReason = "" }
        }
    }

    // MARK: - Interaction Banner

    private var interactionBanner: some View {
        Group {
            if interactions.isEmpty && !activeMedications.isEmpty {
                HStack(spacing: 8) {
                    Image(systemName: "checkmark.shield.fill")
                        .foregroundColor(LiquidGlass.greenPositive)
                    Text("✓ No Interactions Found")
                        .font(.subheadline.weight(.medium))
                        .foregroundColor(.white)
                }
                .frame(maxWidth: .infinity)
                .glassBanner(tint: LiquidGlass.greenPositive.opacity(0.15))
            } else if !interactions.isEmpty {
                let highCount = interactions.filter { $0.severity == 2 }.count
                let medCount = interactions.filter { $0.severity == 1 }.count
                let lowCount = interactions.filter { $0.severity == 0 }.count
                
                VStack(alignment: .leading, spacing: 6) {
                    HStack(spacing: 8) {
                        Image(systemName: "exclamationmark.triangle.fill")
                            .foregroundColor(.white)
                        Text("⚠ \(interactions.count) Interaction\(interactions.count == 1 ? "" : "s") Detected")
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
            } else if engineWrapper.medications.isEmpty {
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
            ForEach(interactions) { interaction in
                HStack(alignment: .top, spacing: 12) {
                    Image(systemName: "shield.fill")
                        .foregroundColor(interaction.severity == 2 ? LiquidGlass.redCritical : LiquidGlass.amberWarning)
                        .font(.title3)
                    
                    VStack(alignment: .leading, spacing: 6) {
                        HStack {
                            Text("\(interaction.drugA) + \(interaction.drugB)")
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
            Text("Active Medications · \(activeMedications.count) drug\(activeMedications.count == 1 ? "" : "s")")
                .font(.subheadline.weight(.medium))
                .foregroundColor(.white.opacity(0.65))
                .padding(.horizontal, 4)
            
            ForEach(activeMedications) { med in
                MedicationCard(medication: med)
                    .contextMenu {
                        Button {
                            selectedMedication = med
                            showEndReasonDialog = true
                        } label: {
                            Label("End Medication", systemImage: "stop.circle")
                        }
                        Button(role: .destructive) {
                            Task {
                                let _ = await engineWrapper.discontinueMedication(id: med.id, reason: "Removed by user")
                            }
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
            Text("Ended · \(inactiveMedications.count) medication\(inactiveMedications.count == 1 ? "" : "s")")
                .font(.caption.weight(.medium))
                .foregroundColor(.white.opacity(0.4))
                .padding(.horizontal, 4)
            
            ForEach(inactiveMedications) { med in
                MedicationCard(medication: med)
                    .opacity(0.75)
            }
        }
    }

    // MARK: - FABs

    private var floatingActionButtons: some View {
        HStack(spacing: 12) {
            Button(action: { showingInteractions = true }) {
                HStack(spacing: 6) {
                    Image(systemName: "magnifyingglass")
                        .font(.system(size: 14))
                    Text("Check Interactions")
                        .font(.caption.weight(.medium))
                }
                .foregroundColor(.white)
            }
            .glassPill(tint: LiquidGlass.tealPrimary.opacity(0.15))
            .disabled(interactions.isEmpty)
            
            Button(action: { showingAddSheet = true }) {
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

    private func checkAllInteractions() async {
        let rxCuis = engineWrapper.medications
            .filter { $0.isActive && $0.rxCUI != nil }
            .compactMap { $0.rxCUI }
        guard rxCuis.count >= 2 else { interactions = []; return }
        interactions = await engineWrapper.checkInteractions(rxCuis: rxCuis)
    }
}

// MARK: - Medication Card (MAUI-style horizontal layout)

struct MedicationCard: View {
    let medication: MedicationInfo

    private var addedByLabel: String {
        switch medication.addedByRole {
        case 1: return "Prescribed"
        case 2: return "OCR Scan"
        default: return "Self-added"
        }
    }

    private var addedByColor: Color {
        switch medication.addedByRole {
        case 1: return Color(red: 168/255, green: 85/255, blue: 247/255) // purple
        case 2: return Color(red: 245/255, green: 158/255, blue: 11/255) // amber
        default: return LiquidGlass.tealPrimary
        }
    }

    private var routeDisplay: String? {
        switch medication.route {
        case 0: return "Oral"
        case 1: return "IV"
        case 2: return "IM"
        case 3: return "Topical"
        case 4: return "Inhaled"
        case 5: return "Sublingual"
        default: return nil
        }
    }

    var body: some View {
        HStack(spacing: 12) {
            // Left side
            VStack(alignment: .leading, spacing: 4) {
                HStack(spacing: 6) {
                    Text(medication.name)
                        .font(.system(size: 15, weight: .semibold))
                        .foregroundColor(.white)
                    if let dosage = medication.dosage {
                        Text(dosage)
                            .font(.caption)
                            .foregroundColor(.white.opacity(0.5))
                    }
                }
                
                if let frequency = medication.frequency {
                    Text(frequency)
                        .font(.system(size: 12))
                        .foregroundColor(.white.opacity(0.5))
                }
                
                if let reason = medication.reason {
                    Text(reason)
                        .font(.system(size: 11))
                        .foregroundColor(.white.opacity(0.35))
                        .lineLimit(1)
                }
                
                if !medication.isActive, let endDate = medication.endDate {
                    Text("Ended \(endDate.formatted(date: .abbreviated, time: .omitted))")
                        .font(.caption2)
                        .foregroundColor(.white.opacity(0.35))
                }
            }
            
            Spacer()
            
            // Right side — chips
            VStack(alignment: .trailing, spacing: 6) {
                if let routeLabel = routeDisplay {
                    Text(routeLabel)
                        .font(.system(size: 10, weight: .medium))
                        .foregroundColor(LiquidGlass.tealPrimary)
                        .padding(.horizontal, 8)
                        .padding(.vertical, 3)
                        .background {
                            RoundedRectangle(cornerRadius: LiquidGlass.radiusChip)
                                .stroke(LiquidGlass.tealPrimary.opacity(0.4), lineWidth: 1)
                                .background(RoundedRectangle(cornerRadius: LiquidGlass.radiusChip).fill(LiquidGlass.tealPrimary.opacity(0.1)))
                        }
                }
                
                Text(addedByLabel)
                    .font(.system(size: 10, weight: .medium))
                    .foregroundColor(addedByColor)
                    .padding(.horizontal, 8)
                    .padding(.vertical, 3)
                    .background {
                        RoundedRectangle(cornerRadius: LiquidGlass.radiusChip)
                            .stroke(addedByColor.opacity(0.4), lineWidth: 1)
                            .background(RoundedRectangle(cornerRadius: LiquidGlass.radiusChip).fill(addedByColor.opacity(0.1)))
                    }
            }
            
            Image(systemName: "chevron.right")
                .font(.caption)
                .foregroundColor(.white.opacity(0.3))
        }
        .glassCard()
    }
}

// MARK: - Medication Safety Badge

struct MedicationSafetyBadge: View {
    let severity: Int

    private var label: String {
        switch severity {
        case 2: return "High"
        case 1: return "Medium"
        default: return "Low"
        }
    }

    private var color: Color {
        switch severity {
        case 2: return LiquidGlass.redCritical
        case 1: return LiquidGlass.amberWarning
        default: return LiquidGlass.greenPositive
        }
    }

    var body: some View {
        Text(label)
            .font(.system(size: 10, weight: .bold))
            .foregroundColor(color)
            .padding(.horizontal, 8)
            .padding(.vertical, 3)
            .background {
                RoundedRectangle(cornerRadius: LiquidGlass.radiusChip)
                    .fill(color.opacity(0.15))
            }
    }
}

// MARK: - Interaction Banner

struct InteractionBanner: View {
    let interactions: [MedicationInteractionInfo]

    private var highSeverity: [MedicationInteractionInfo] {
        interactions.filter { $0.severity == 2 }
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            Label("\(interactions.count) Interaction\(interactions.count == 1 ? "" : "s") Found",
                  systemImage: "exclamationmark.triangle.fill")
                .font(.headline)
                .foregroundColor(.white)

            if let worst = highSeverity.first {
                Text("\(worst.drugA) + \(worst.drugB): \(worst.description)")
                    .font(.caption)
                    .foregroundColor(.white.opacity(0.9))
                    .lineLimit(2)
            }
        }
        .glassBanner(tint: highSeverity.isEmpty ? LiquidGlass.amberWarning : LiquidGlass.redCritical)
    }
}

// MARK: - Add Medication Sheet

struct AddMedicationSheet: View {
    @EnvironmentObject var engineWrapper: MobileEngineWrapper
    @Environment(\.dismiss) private var dismiss
    @Binding var drugSearchResults: [DrugSearchResult]
    @Binding var searchText: String

    @State private var name = ""
    @State private var dosage = ""
    @State private var frequency = ""
    @State private var instructions = ""
    @State private var selectedRxCUI: String?
    @State private var isSaving = false

    var body: some View {
        NavigationView {
            Form {
                Section("Drug Name") {
                    TextField("Medication name", text: $name)
                        .onChange(of: name) { _, newValue in
                            Task {
                                guard newValue.count >= 3 else { drugSearchResults = []; return }
                                drugSearchResults = await engineWrapper.searchDrugs(query: newValue)
                            }
                        }

                    if !drugSearchResults.isEmpty {
                        ForEach(drugSearchResults) { result in
                            Button {
                                name = result.name
                                selectedRxCUI = result.rxCUI
                                drugSearchResults = []
                            } label: {
                                VStack(alignment: .leading) {
                                    Text(result.name).font(.subheadline)
                                    if let syn = result.synonym {
                                        Text(syn).font(.caption).foregroundColor(.secondary)
                                    }
                                }
                            }
                        }
                    }
                }

                Section("Details") {
                    TextField("Dosage (e.g. 500mg)", text: $dosage)
                    TextField("Frequency (e.g. twice daily)", text: $frequency)
                    TextField("Instructions", text: $instructions)
                }
            }
            .navigationTitle("Add Medication")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .cancellationAction) { Button("Cancel") { dismiss() } }
                ToolbarItem(placement: .confirmationAction) {
                    Button("Save") {
                        Task { await save() }
                    }
                    .disabled(name.isEmpty || isSaving)
                    .liquidGlassButtonStyle()
                }
            }
        }
    }

    private func save() async {
        isSaving = true
        let input = AddMedicationInput(
            name: name,
            dosage: dosage.isEmpty ? nil : dosage,
            frequency: frequency.isEmpty ? nil : frequency,
            route: 0,
            rxCUI: selectedRxCUI,
            instructions: instructions.isEmpty ? nil : instructions,
            reason: nil,
            prescribedBy: nil
        )
        let _ = await engineWrapper.addMedication(input)
        dismiss()
    }
}

// MARK: - Interactions Sheet

struct InteractionsSheet: View {
    let interactions: [MedicationInteractionInfo]
    @Environment(\.dismiss) private var dismiss

    var body: some View {
        NavigationView {
            List(interactions) { interaction in
                VStack(alignment: .leading, spacing: 8) {
                    HStack {
                        Text("\(interaction.drugA) + \(interaction.drugB)")
                            .font(.headline)
                        Spacer()
                        MedicationSafetyBadge(severity: interaction.severity)
                    }
                    Text(interaction.description)
                        .font(.subheadline)
                        .foregroundColor(.secondary)
                }
                .padding(.vertical, 4)
            }
            .navigationTitle("Interactions")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .confirmationAction) { Button("Done") { dismiss() } }
            }
        }
    }
}

// MARK: - Empty State

struct EmptyMedicationsView: View {
    var body: some View {
        VStack(spacing: 16) {
            Image(systemName: "pills.fill")
                .font(.system(size: 50))
                .foregroundColor(.white.opacity(0.3))
            Text("No Medications")
                .font(.title3).fontWeight(.semibold)
                .foregroundColor(.white)
            Text("Add your medications to track them and check for interactions.")
                .font(.subheadline).foregroundColor(.white.opacity(0.65))
                .multilineTextAlignment(.center)
        }
        .frame(maxWidth: .infinity)
        .padding(.vertical, 60)
    }
}
