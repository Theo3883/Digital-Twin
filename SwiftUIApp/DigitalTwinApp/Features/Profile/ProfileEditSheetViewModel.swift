import Foundation

@MainActor
final class ProfileEditSheetViewModel: ObservableObject {
    @Published var bloodType: String = ""
    @Published var allergies: String = ""
    @Published var weight: String = ""
    @Published var height: String = ""
    @Published var systolic: String = ""
    @Published var diastolic: String = ""
    @Published var cholesterol: String = ""
    @Published var cnp: String = ""
    @Published private(set) var isSaving: Bool = false

    private let repository: ProfileRepository
    private let initialPatient: PatientInfo?

    init(repository: ProfileRepository, patient: PatientInfo?) {
        self.repository = repository
        self.initialPatient = patient
        populateFields(from: patient)
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

    func save() async {
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
        await repository.updatePatientProfile(input)
    }
}

