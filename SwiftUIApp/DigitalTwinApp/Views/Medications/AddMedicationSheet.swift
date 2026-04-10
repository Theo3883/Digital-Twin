import SwiftUI

struct AddMedicationSheet: View {
    @Environment(\.dismiss) private var dismiss
    @StateObject private var viewModel: AddMedicationSheetViewModel

    @State private var name = ""
    @State private var dosage = ""
    @State private var frequency = ""
    @State private var instructions = ""
    @State private var selectedRxCui: String?
    @State private var isSaving = false

    init(viewModel: AddMedicationSheetViewModel) {
        _viewModel = StateObject(wrappedValue: viewModel)
    }

    var body: some View {
        NavigationView {
            Form {
                Section("Drug Name") {
                    TextField("Medication name", text: $name)
                        .onChange(of: name) { _, newValue in
                            Task { await viewModel.search(query: newValue) }
                        }

                    if !viewModel.searchResults.isEmpty {
                        ForEach(viewModel.searchResults) { result in
                            Button {
                                name = result.name
                                selectedRxCui = result.rxCui
                                viewModel.clearResults()
                            } label: {
                                VStack(alignment: .leading) {
                                    Text(result.name).font(.subheadline)
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
            dosage: dosage.isEmpty ? "" : dosage,
            frequency: frequency.isEmpty ? nil : frequency,
            route: 0,
            rxCui: selectedRxCui,
            instructions: instructions.isEmpty ? nil : instructions,
            reason: nil,
            prescribedByUserId: nil,
            startDate: nil,
            addedByRole: 0
        )
        let _ = await viewModel.add(input: input)
        dismiss()
    }
}

