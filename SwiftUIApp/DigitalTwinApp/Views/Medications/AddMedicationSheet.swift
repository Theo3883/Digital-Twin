import SwiftUI

struct AddMedicationSheet: View {
    @Environment(\.dismiss) private var dismiss
    @StateObject private var viewModel: AddMedicationSheetViewModel

    @State private var name = ""
    @State private var dosage = ""
    @State private var frequency = ""
    @State private var instructions = ""
    @State private var selectedRxCUI: String?
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
                                selectedRxCUI = result.rxCUI
                                viewModel.clearResults()
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
        let _ = await viewModel.add(input: input)
        dismiss()
    }
}

