import Foundation
import CoreML

@MainActor
final class DocumentTypeMlClassifier {
    static let shared = DocumentTypeMlClassifier()

    private let model: MLModel?

    private init() {
        // Attempt to locate compiled model in bundle resources
        if let url = Bundle.main.url(forResource: "doc_type_classifier_v3", withExtension: "mlmodelc", subdirectory: "MLModels") {
            self.model = try? MLModel(contentsOf: url)
        } else if let url = Bundle.main.url(forResource: "doc_type_classifier_v3", withExtension: "mlmodel", subdirectory: "MLModels") {
            // Fallback: compile at runtime
            if let compiled = try? MLModel.compileModel(at: url) {
                self.model = try? MLModel(contentsOf: compiled)
            } else { self.model = nil }
        } else {
            self.model = nil
        }
    }

    /// Classify document text. Returns (type, confidence) or nil on failure.
    func classify(ocrText: String) -> (type: String, confidence: Float)? {
        guard let model = model else { return nil }

        // Prepare input feature provider. Common name used is "text" but we attempt to inspect the model.
        let inputName = model.modelDescription.inputDescriptionsByName.keys.first ?? "text"
        let inputValue = MLFeatureValue(string: ocrText)
        let provider = try? MLDictionaryFeatureProvider(dictionary: [inputName: inputValue])
        guard let p = provider else { return nil }

        guard let out = try? model.prediction(from: p) else { return nil }

        // Try common output names
        if let prob = out.featureValue(for: "confidence")?.floatValue,
           let type = out.featureValue(for: "classLabel")?.stringValue {
            return (type, prob)
        }

        // Fallback: pick first string-valued output and a numeric confidence if present
        if let firstStringKey = out.featureNames.first(where: { out.featureValue(for: $0)?.stringValue != nil }),
           let type = out.featureValue(for: firstStringKey)?.stringValue {
            let conf = out.featureNames.compactMap { out.featureValue(for: $0)?.floatValue }.first ?? 1.0
            return (type, conf)
        }

        return nil
    }
}
