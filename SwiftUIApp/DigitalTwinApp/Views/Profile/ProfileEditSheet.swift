import SwiftUI

struct ProfileEditSheet: View {
    @Environment(\.dismiss) private var dismiss
    @StateObject private var viewModel: ProfileEditSheetViewModel
    private let isMandatorySetup: Bool
    private let onCancel: (() async -> Void)?
    private let onSave: (() async -> Void)?

    init(
        viewModel: ProfileEditSheetViewModel,
        isMandatorySetup: Bool = false,
        onCancel: (() async -> Void)? = nil,
        onSave: (() async -> Void)? = nil
    ) {
        _viewModel = StateObject(wrappedValue: viewModel)
        self.isMandatorySetup = isMandatorySetup
        self.onCancel = onCancel
        self.onSave = onSave
    }

    var body: some View {
        NavigationView {
            Form {
                Section("Name") {
                    TextField("First Name", text: $viewModel.firstName)
                    if let firstNameError = viewModel.firstNameError {
                        Text(firstNameError)
                            .font(.footnote)
                            .foregroundColor(.red)
                    }

                    TextField("Last Name", text: $viewModel.lastName)
                    if let lastNameError = viewModel.lastNameError {
                        Text(lastNameError)
                            .font(.footnote)
                            .foregroundColor(.red)
                    }
                }

                Section("Contact") {
                    TextField("Phone Number", text: $viewModel.phone)
                        .keyboardType(.phonePad)
                    if let phoneError = viewModel.phoneError {
                        Text(phoneError)
                            .font(.footnote)
                            .foregroundColor(.red)
                    }
                }

                Section("Address") {
                    TextField("Address", text: $viewModel.address)
                    if let addressError = viewModel.addressError {
                        Text(addressError)
                            .font(.footnote)
                            .foregroundColor(.red)
                    }

                    TextField("City", text: $viewModel.city)
                    if let cityError = viewModel.cityError {
                        Text(cityError)
                            .font(.footnote)
                            .foregroundColor(.red)
                    }

                    TextField("Country", text: $viewModel.country)
                    if let countryError = viewModel.countryError {
                        Text(countryError)
                            .font(.footnote)
                            .foregroundColor(.red)
                    }
                }

                Section("Personal") {
                    DatePicker(
                        "Date of Birth",
                        selection: $viewModel.dateOfBirth,
                        in: ...Date(),
                        displayedComponents: .date
                    )
                }

                if let errorMessage = viewModel.errorMessage {
                    Section {
                        Text(errorMessage)
                            .font(.footnote)
                            .foregroundColor(.red)
                    }
                }
            }
            .navigationTitle(isMandatorySetup ? "Complete User Profile" : "Edit User Profile")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .cancellationAction) {
                    Button("Cancel") {
                        if let onCancel {
                            Task {
                                await onCancel()
                                dismiss()
                            }
                        } else {
                            dismiss()
                        }
                    }
                }
                ToolbarItem(placement: .confirmationAction) {
                    Button("Save") {
                        Task {
                            let didSave = await viewModel.save()
                            guard didSave else { return }

                            if let onSave {
                                await onSave()
                            }

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
        .interactiveDismissDisabled(isMandatorySetup)
    }
}

struct PatientProfileEditSheet: View {
    @Environment(\.dismiss) private var dismiss
    @StateObject private var viewModel: PatientProfileEditSheetViewModel
    private let isMandatorySetup: Bool
    private let onCancel: (() async -> Void)?

    init(
        viewModel: PatientProfileEditSheetViewModel,
        isMandatorySetup: Bool = false,
        onCancel: (() async -> Void)? = nil
    ) {
        _viewModel = StateObject(wrappedValue: viewModel)
        self.isMandatorySetup = isMandatorySetup
        self.onCancel = onCancel
    }

    var body: some View {
        NavigationView {
            Form {
                Section("Personal") {
                    VStack(alignment: .leading, spacing: 6) {
                        Text("Blood Type")
                            .font(.subheadline)

                        Picker("Blood Type", selection: $viewModel.bloodType) {
                            Text("Select blood type").tag("")
                            ForEach(PatientProfileEditSheetViewModel.bloodTypes, id: \.self) { bloodType in
                                Text(bloodType).tag(bloodType)
                            }
                        }
                        .pickerStyle(.wheel)
                        .frame(height: 120)
                        .clipped()
                    }

                    TextField("CNP", text: $viewModel.cnp)
                }
                Section("Measurements") {
                    TextField("Weight (kg)", text: $viewModel.weight)
                        .keyboardType(.decimalPad)
                    TextField("Height (cm)", text: $viewModel.height)
                        .keyboardType(.decimalPad)
                }
                Section("Vitals") {
                    HStack {
                        TextField("Systolic", text: $viewModel.systolic)
                            .keyboardType(.numberPad)
                        Text("/")
                        TextField("Diastolic", text: $viewModel.diastolic)
                            .keyboardType(.numberPad)
                    }
                    TextField("Cholesterol (mg/dL)", text: $viewModel.cholesterol)
                        .keyboardType(.decimalPad)
                }
                Section("Other") {
                    TextField("Allergies", text: $viewModel.allergies)
                }

                if isMandatorySetup && !viewModel.canSaveMandatory {
                    Section {
                        Text("Blood type, CNP, weight, height, and allergies are required.")
                            .font(.footnote)
                            .foregroundColor(.red)
                    }
                }

                if let errorMessage = viewModel.errorMessage {
                    Section {
                        Text(errorMessage)
                            .font(.footnote)
                            .foregroundColor(.red)
                    }
                }
            }
            .navigationTitle(isMandatorySetup ? "Create Patient Profile" : "Edit Patient Profile")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .cancellationAction) {
                    Button("Cancel") {
                        if let onCancel {
                            Task {
                                await onCancel()
                                dismiss()
                            }
                        } else {
                            dismiss()
                        }
                    }
                }
                ToolbarItem(placement: .confirmationAction) {
                    Button("Save") {
                        Task {
                            let didSave = await viewModel.save()
                            if didSave {
                                dismiss()
                            }
                        }
                    }
                    .disabled(viewModel.isSaving || (isMandatorySetup && !viewModel.canSaveMandatory))
                    .liquidGlassButtonStyle()
                }
            }
        }
        .presentationDetents([.large])
        .presentationDragIndicator(.visible)
        .interactiveDismissDisabled(isMandatorySetup)
    }
}

