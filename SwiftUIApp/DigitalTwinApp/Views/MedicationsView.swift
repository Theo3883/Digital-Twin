import SwiftUI

struct MedicationsView: View {
    @EnvironmentObject var engineWrapper: MobileEngineWrapper
    @State private var showingAddSheet = false
    @State private var searchText = ""
    @State private var drugSearchResults: [DrugSearchResult] = []
    @State private var interactions: [MedicationInteractionInfo] = []
    @State private var showingInteractions = false

    private var activeMedications: [MedicationInfo] {
        engineWrapper.medications.filter { $0.isActive }
    }

    private var inactiveMedications: [MedicationInfo] {
        engineWrapper.medications.filter { !$0.isActive }
    }

    var body: some View {
        NavigationView {
            ScrollView {
                VStack(spacing: 16) {
                    // Interaction warnings
                    if !interactions.isEmpty {
                        InteractionBanner(interactions: interactions)
                    }

                    // Active medications
                    if !activeMedications.isEmpty {
                        VStack(alignment: .leading, spacing: 12) {
                            Text("Active Medications")
                                .glassSectionHeader()

                            ForEach(activeMedications) { med in
                                MedicationCard(medication: med) {
                                    Task {
                                        let _ = await engineWrapper.discontinueMedication(id: med.id, reason: nil)
                                    }
                                }
                            }
                        }
                    }

                    // Inactive medications
                    if !inactiveMedications.isEmpty {
                        VStack(alignment: .leading, spacing: 12) {
                            Text("Past Medications")
                                .glassSectionHeader()

                            ForEach(inactiveMedications) { med in
                                MedicationCard(medication: med, onDiscontinue: nil)
                                    .opacity(0.7)
                            }
                        }
                    }

                    if engineWrapper.medications.isEmpty {
                        EmptyMedicationsView()
                    }

                    Spacer(minLength: 100)
                }
                .padding()
            }
            .navigationTitle("Medications")
            .liquidGlassNavigationStyle()
            .searchable(text: $searchText, prompt: "Search drugs...")
            .onChange(of: searchText) { _, newValue in
                Task {
                    guard newValue.count >= 3 else { drugSearchResults = []; return }
                    drugSearchResults = await engineWrapper.searchDrugs(query: newValue)
                }
            }
            .toolbar {
                ToolbarItem(placement: .navigationBarTrailing) {
                    Button(action: { showingAddSheet = true }) {
                        Image(systemName: "plus")
                    }
                    .liquidGlassButtonStyle()
                }
                ToolbarItem(placement: .navigationBarLeading) {
                    Button(action: { showingInteractions = true }) {
                        Image(systemName: "exclamationmark.triangle")
                    }
                    .liquidGlassButtonStyle()
                    .opacity(interactions.isEmpty ? 0.3 : 1)
                    .disabled(interactions.isEmpty)
                }
            }
            .sheet(isPresented: $showingAddSheet) {
                AddMedicationSheet(drugSearchResults: $drugSearchResults, searchText: $searchText)
            }
            .sheet(isPresented: $showingInteractions) {
                InteractionsSheet(interactions: interactions)
            }
            .task {
                await engineWrapper.loadMedications()
                await checkAllInteractions()
            }
        }
    }

    private func checkAllInteractions() async {
        let rxCuis = engineWrapper.medications
            .filter { $0.isActive && $0.rxCUI != nil }
            .compactMap { $0.rxCUI }
        guard rxCuis.count >= 2 else { interactions = []; return }
        interactions = await engineWrapper.checkInteractions(rxCuis: rxCuis)
    }
}

// MARK: - Medication Card

struct MedicationCard: View {
    let medication: MedicationInfo
    var onDiscontinue: (() -> Void)?

    var body: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack {
                VStack(alignment: .leading, spacing: 4) {
                    Text(medication.name)
                        .font(.headline)
                    if let dosage = medication.dosage {
                        Text(dosage)
                            .font(.subheadline)
                            .foregroundColor(.secondary)
                    }
                }
                Spacer()
                Text(medication.statusDisplay)
                    .glassChip(tint: medication.isActive ? LiquidGlass.greenPositive : .gray)
            }

            if let frequency = medication.frequency {
                Label(frequency, systemImage: "clock")
                    .font(.caption)
                    .foregroundColor(.secondary)
            }

            if let instructions = medication.instructions {
                Text(instructions)
                    .font(.caption)
                    .foregroundColor(.secondary)
                    .lineLimit(2)
            }

            HStack {
                Text("Started \(medication.startDate.formatted(date: .abbreviated, time: .omitted))")
                    .font(.caption2)
                    .foregroundColor(.secondary)
                Spacer()
                if medication.isActive, let onDiscontinue {
                    Button("Discontinue") { onDiscontinue() }
                        .font(.caption)
                        .buttonStyle(.glass)
                }
            }
        }
        .glassCard()
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
                        Text(interaction.severityDisplay)
                            .glassChip(tint: interaction.severity == 2 ? LiquidGlass.redCritical :
                                             interaction.severity == 1 ? LiquidGlass.amberWarning :
                                             LiquidGlass.greenPositive)
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
                .foregroundColor(.secondary)
            Text("No Medications")
                .font(.title3).fontWeight(.semibold)
            Text("Add your medications to track them and check for interactions.")
                .font(.subheadline).foregroundColor(.secondary)
                .multilineTextAlignment(.center)
        }
        .frame(maxWidth: .infinity)
        .padding(.vertical, 60)
    }
}
