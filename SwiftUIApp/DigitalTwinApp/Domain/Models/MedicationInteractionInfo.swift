import Foundation

struct MedicationInteractionInfo: Codable, Identifiable {
    let drugARxCui: String
    let drugBRxCui: String
    let severity: Int    // InteractionSeverity enum: None=0, Low=1, Medium=2, High=3
    let description: String

    private enum CodingKeys: String, CodingKey {
        case drugARxCui
        case drugBRxCui
        case severity
        case description
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        drugARxCui = try container.decode(String.self, forKey: .drugARxCui)
        drugBRxCui = try container.decode(String.self, forKey: .drugBRxCui)
        description = try container.decode(String.self, forKey: .description)

        if let numericSeverity = try? container.decode(Int.self, forKey: .severity) {
            severity = numericSeverity
            return
        }

        if let textualSeverity = try? container.decode(String.self, forKey: .severity) {
            switch textualSeverity.trimmingCharacters(in: .whitespacesAndNewlines).lowercased() {
            case "none": severity = 0
            case "low": severity = 1
            case "medium": severity = 2
            case "high": severity = 3
            default: severity = 0
            }
            return
        }

        severity = 0
    }

    func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(drugARxCui, forKey: .drugARxCui)
        try container.encode(drugBRxCui, forKey: .drugBRxCui)
        try container.encode(severity, forKey: .severity)
        try container.encode(description, forKey: .description)
    }

    var id: String { "\(drugARxCui)-\(drugBRxCui)" }

    var severityDisplay: String {
        switch severity {
        case 0: return "None"
        case 1: return "Low"
        case 2: return "Medium"
        case 3: return "High"
        default: return "Unknown"
        }
    }

    var severityColor: String {
        switch severity {
        case 0: return "gray"
        case 1: return "green"
        case 2: return "orange"
        case 3: return "red"
        default: return "gray"
        }
    }

    func displayPair(using medications: [MedicationInfo]) -> String {
        let parsedNames = parsedNamesFromDescription()
        let left = resolveName(for: drugARxCui, medications: medications) ?? parsedNames?.left ?? drugARxCui
        let right = resolveName(for: drugBRxCui, medications: medications) ?? parsedNames?.right ?? drugBRxCui
        return "\(left) + \(right)"
    }

    private func resolveName(for rxCui: String, medications: [MedicationInfo]) -> String? {
        medications
            .first(where: { $0.rxCui?.caseInsensitiveCompare(rxCui) == .orderedSame })?
            .name
    }

    private func parsedNamesFromDescription() -> (left: String, right: String)? {
        let pattern = #"between\s+(.+?)\s+and\s+(.+?)(?:[.;]|$)"#
        guard let regex = try? NSRegularExpression(pattern: pattern, options: [.caseInsensitive]) else {
            return nil
        }

        let nsRange = NSRange(description.startIndex..., in: description)
        guard let match = regex.firstMatch(in: description, options: [], range: nsRange), match.numberOfRanges >= 3,
              let leftRange = Range(match.range(at: 1), in: description),
              let rightRange = Range(match.range(at: 2), in: description)
        else {
            return nil
        }

        let left = description[leftRange].trimmingCharacters(in: .whitespacesAndNewlines)
        let right = description[rightRange].trimmingCharacters(in: .whitespacesAndNewlines)
        if left.isEmpty || right.isEmpty {
            return nil
        }

        return (String(left), String(right))
    }
}

