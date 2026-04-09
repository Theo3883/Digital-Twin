import SwiftUI

struct ProfileEditSheet: View {
    @Environment(\.dismiss) private var dismiss
    @StateObject private var viewModel: ProfileEditSheetViewModel

    init(viewModel: ProfileEditSheetViewModel) {
        _viewModel = StateObject(wrappedValue: viewModel)
    }

    var body: some View {
        NavigationView {
            Form {
                Section("Personal") {
                    TextField("Blood Type (e.g. A+)", text: $viewModel.bloodType)
                    TextField("CNP", text: $viewModel.cnp)
                }
                Section("Measurements") {
                    TextField("Weight (lbs)", text: $viewModel.weight).keyboardType(.decimalPad)
                    TextField("Height (in)", text: $viewModel.height).keyboardType(.decimalPad)
                }
                Section("Vitals") {
                    HStack {
                        TextField("Systolic", text: $viewModel.systolic).keyboardType(.numberPad)
                        Text("/")
                        TextField("Diastolic", text: $viewModel.diastolic).keyboardType(.numberPad)
                    }
                    TextField("Cholesterol (mg/dL)", text: $viewModel.cholesterol).keyboardType(.decimalPad)
                }
                Section("Other") {
                    TextField("Allergies", text: $viewModel.allergies)
                }
            }
            .navigationTitle("Edit Profile")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .cancellationAction) { Button("Cancel") { dismiss() } }
                ToolbarItem(placement: .confirmationAction) {
                    Button("Save") {
                        Task {
                            await viewModel.save()
                            dismiss()
                        }
                    }
                    .disabled(viewModel.isSaving)
                    .liquidGlassButtonStyle()
                }
            }
        }
        .presentationDetents([.large])
        .presentationDragIndicator(.visible)
    }
}

