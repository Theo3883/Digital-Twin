import Foundation

@MainActor
final class ProfileEditSheetViewModel: ObservableObject {
    @Published var firstName: String = ""
    @Published var lastName: String = ""
    @Published var phone: String = ""
    @Published var address: String = ""
    @Published var city: String = ""
    @Published var country: String = ""
    @Published var dateOfBirth: Date = Calendar.current.date(byAdding: .year, value: -30, to: Date()) ?? Date()
    @Published var showValidationErrors: Bool = false
    @Published var errorMessage: String?
    @Published private(set) var isSaving: Bool = false

    private let repository: ProfileRepository

    init(repository: ProfileRepository, user: UserInfo?) {
        self.repository = repository
        populateFields(from: user)
    }

    var canSave: Bool {
        !trimmed(firstName).isEmpty
            && !trimmed(lastName).isEmpty
            && !trimmed(phone).isEmpty
            && !trimmed(address).isEmpty
            && !trimmed(city).isEmpty
            && !trimmed(country).isEmpty
    }

    var firstNameError: String? {
        showValidationErrors && trimmed(firstName).isEmpty ? "First name is required." : nil
    }

    var lastNameError: String? {
        showValidationErrors && trimmed(lastName).isEmpty ? "Last name is required." : nil
    }

    var phoneError: String? {
        showValidationErrors && trimmed(phone).isEmpty ? "Phone number is required." : nil
    }

    var addressError: String? {
        showValidationErrors && trimmed(address).isEmpty ? "Address is required." : nil
    }

    var cityError: String? {
        showValidationErrors && trimmed(city).isEmpty ? "City is required." : nil
    }

    var countryError: String? {
        showValidationErrors && trimmed(country).isEmpty ? "Country is required." : nil
    }

    private func populateFields(from user: UserInfo?) {
        guard let user else { return }

        firstName = user.firstName ?? ""
        lastName = user.lastName ?? ""
        phone = user.phone ?? ""
        address = user.address ?? ""
        city = user.city ?? ""
        country = user.country ?? ""

        if let dob = user.dateOfBirth {
            dateOfBirth = dob
        }
    }

    func save() async -> Bool {
        showValidationErrors = true

        guard canSave else {
            errorMessage = nil
            return false
        }

        isSaving = true
        defer { isSaving = false }

        let input = UserUpdateInfo(
            firstName: trimmed(firstName),
            lastName: trimmed(lastName),
            phone: optionalTrimmed(phone),
            address: optionalTrimmed(address),
            city: optionalTrimmed(city),
            country: optionalTrimmed(country),
            dateOfBirth: dateOfBirth
        )

        let success = await repository.updateUserProfile(input)
        errorMessage = success ? nil : "Failed to save your user profile."
        return success
    }

    private func trimmed(_ value: String) -> String {
        value.trimmingCharacters(in: .whitespacesAndNewlines)
    }

    private func optionalTrimmed(_ value: String) -> String? {
        let value = trimmed(value)
        return value.isEmpty ? nil : value
    }
}

@MainActor
final class PatientProfileEditSheetViewModel: ObservableObject {
    static let bloodTypes: [String] = [
        "A+", "A-", "B+", "B-", "AB+", "AB-", "O+", "O-"
    ]

    @Published var bloodType: String = ""
    @Published var allergies: String = ""
    @Published var weight: String = ""
    @Published var height: String = ""
    @Published var systolic: String = ""
    @Published var diastolic: String = ""
    @Published var cholesterol: String = ""
    @Published var cnp: String = ""
    @Published var errorMessage: String?
    @Published private(set) var isSaving: Bool = false

    private let repository: ProfileRepository

    init(repository: ProfileRepository, patient: PatientInfo?) {
        self.repository = repository
        populateFields(from: patient)
    }

    var canSaveMandatory: Bool {
        !trimmed(bloodType).isEmpty
            && !trimmed(cnp).isEmpty
            && parsedPositiveDouble(weight) != nil
            && parsedPositiveDouble(height) != nil
            && !trimmed(allergies).isEmpty
    }

    private func populateFields(from patient: PatientInfo?) {
        guard let p = patient else { return }
        bloodType = p.bloodType ?? ""
        cnp = p.cnp ?? ""
        weight = p.weight.map { String(format: "%.1f", $0) } ?? ""
        height = p.height.map { String(format: "%.1f", $0) } ?? ""
        systolic = p.bloodPressureSystolic.map { "\($0)" } ?? ""
        diastolic = p.bloodPressureDiastolic.map { "\($0)" } ?? ""
        cholesterol = p.cholesterol.map { String(format: "%.1f", $0) } ?? ""
        allergies = p.allergies ?? ""
    }

    func save() async -> Bool {
        isSaving = true
        defer { isSaving = false }

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

        let success = await repository.updatePatientProfile(input)
        errorMessage = success ? nil : "Failed to save your patient profile."
        return success
    }

    private func trimmed(_ value: String) -> String {
        value.trimmingCharacters(in: .whitespacesAndNewlines)
    }

    private func parsedPositiveDouble(_ value: String) -> Double? {
        guard let number = Double(trimmed(value)), number > 0 else { return nil }
        return number
    }
}

