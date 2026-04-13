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
    @State private var saveErrorMessage: String?

    init(viewModel: AddMedicationSheetViewModel) {
        _viewModel = StateObject(wrappedValue: viewModel)
    }

    var body: some View {
        NavigationStack {
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
                    .buttonStyle(.borderedProminent)
                    .buttonBorderShape(.capsule)
                    .tint(.teal)
                }
            }
        }
        .alert(
            "Unable to Add Medication",
            isPresented: Binding(
                get: { saveErrorMessage != nil },
                set: { if !$0 { saveErrorMessage = nil } }
            )
        ) {
            Button("OK", role: .cancel) {
                saveErrorMessage = nil
            }
        } message: {
            Text(saveErrorMessage ?? "")
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
        let result = await viewModel.add(input: input)
        isSaving = false

        if result.success {
            dismiss()
            return
        }

        saveErrorMessage = result.error ?? "Medication could not be added."
    }
}

