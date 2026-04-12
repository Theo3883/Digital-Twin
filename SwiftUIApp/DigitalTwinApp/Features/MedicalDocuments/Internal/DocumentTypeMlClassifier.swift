import CoreML
import Foundation
import NaturalLanguage

enum DocumentTypeMlClassifier {
    private static let knownLabels: Set<String> = [
        "ConsultationNote", "Discharge", "EcgReport", "GenericClinicForm",
        "ImagingReport", "LabResult", "MedicalCertificate", "OperativeReport",
        "Prescription", "Referral"
    ]

    private static let cacheLock = NSLock()
    nonisolated(unsafe) private static var cachedModel: NLModel?

    private static func loadModel() -> NLModel? {
        cacheLock.lock()
        defer { cacheLock.unlock() }
        if let existing = cachedModel { return existing }
        guard let url = Bundle.main.url(forResource: "doc_type_classifier_v1", withExtension: "mlmodelc") else {
            return nil
        }
        guard let mlModel = try? MLModel(contentsOf: url),
              let nlModel = try? NLModel(mlModel: mlModel) else { return nil }
        cachedModel = nlModel
        return nlModel
    }

    static func classifyDocumentType(ocrText: String) async -> (type: String, confidence: Float)? {
        let trimmed = ocrText.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else { return nil }
        guard let model = loadModel() else { return nil }

        guard let label = model.predictedLabel(for: trimmed), !label.isEmpty else { return nil }
        let conf: Float = knownLabels.contains(label) ? 0.90 : 0
        return (label, conf)
    }
}
