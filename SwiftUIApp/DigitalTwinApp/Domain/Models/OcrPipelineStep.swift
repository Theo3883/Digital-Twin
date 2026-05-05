import Foundation

enum OcrPipelineStep: Int, CaseIterable, Identifiable {
    case idle
    case scanning
    case recognizing
    case classifying
    case extractingIdentity
    case validatingIdentity
    case buildingStructured
    case encryptingAndStoring
    case saving
    case complete
    case failed

    var id: Int { rawValue }

    var label: String {
        switch self {
        case .idle:                return "Ready"
        case .scanning:            return "Scanning"
        case .recognizing:         return "Recognizing Text"
        case .classifying:         return "Classifying"
        case .extractingIdentity:  return "Extracting Identity"
        case .validatingIdentity:  return "Validating Identity"
        case .buildingStructured:  return "Building Structured Data"
        case .encryptingAndStoring:return "Encrypting & Storing"
        case .saving:              return "Saving Document"
        case .complete:            return "Complete"
        case .failed:              return "Failed"
        }
    }

    var icon: String {
        switch self {
        case .idle:                return "doc.text.viewfinder"
        case .scanning:            return "camera.fill"
        case .recognizing:         return "text.viewfinder"
        case .classifying:         return "doc.text.magnifyingglass"
        case .extractingIdentity:  return "person.text.rectangle"
        case .validatingIdentity:  return "checkmark.shield"
        case .buildingStructured:  return "tablecells"
        case .encryptingAndStoring:return "lock.shield"
        case .saving:              return "arrow.down.doc"
        case .complete:            return "checkmark.circle.fill"
        case .failed:              return "xmark.circle.fill"
        }
    }

    var isTerminal: Bool { self == .complete || self == .failed }
}
