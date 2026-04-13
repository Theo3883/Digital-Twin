import Foundation

struct CoachingAdviceSectionInfo: Codable, Hashable {
    let category: String
    let title: String
    let items: [String]
}

struct CoachingAdviceActionInfo: Codable, Hashable {
    let category: String
    let label: String
}

struct CoachingAdviceInfo: Codable {
    let advice: String
    let timestamp: Date
    let schemaVersion: String
    let headline: String
    let summary: String
    let sections: [CoachingAdviceSectionInfo]
    let actions: [CoachingAdviceActionInfo]
    let motivation: String
    let safetyNote: String

    var hasStructuredSections: Bool {
        !sections.isEmpty
    }

    var isDeterministicFallback: Bool {
        let normalizedHeadline = headline.trimmingCharacters(in: .whitespacesAndNewlines)
        let normalizedSummary = summary.trimmingCharacters(in: .whitespacesAndNewlines)

        let isGeneralFallback =
            normalizedHeadline == "Steady habit routine" &&
            normalizedSummary == "Based on available data, choose one movement habit, one sleep habit, and one nutrition habit for today."

        let isEnvironmentFallback =
            normalizedHeadline == "Environment-safe routine" &&
            normalizedSummary == "Based on available data, choose low-pollution windows, move gently, and keep your recovery routine consistent."

        return isGeneralFallback || isEnvironmentFallback
    }

    init(
        advice: String,
        timestamp: Date,
        schemaVersion: String = "1.0",
        headline: String = "",
        summary: String = "",
        sections: [CoachingAdviceSectionInfo] = [],
        actions: [CoachingAdviceActionInfo] = [],
        motivation: String = "",
        safetyNote: String = ""
    ) {
        self.advice = advice
        self.timestamp = timestamp
        self.schemaVersion = schemaVersion
        self.headline = headline
        self.summary = summary
        self.sections = sections
        self.actions = actions
        self.motivation = motivation
        self.safetyNote = safetyNote
    }

    private enum CodingKeys: String, CodingKey {
        case advice
        case timestamp
        case schemaVersion
        case headline
        case summary
        case sections
        case actions
        case motivation
        case safetyNote
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)

        let schemaVersion = try container.decodeIfPresent(String.self, forKey: .schemaVersion) ?? "1.0"
        let headline = try container.decodeIfPresent(String.self, forKey: .headline) ?? ""
        let summary = try container.decodeIfPresent(String.self, forKey: .summary) ?? ""
        let sections = try container.decodeIfPresent([CoachingAdviceSectionInfo].self, forKey: .sections) ?? []
        let actions = try container.decodeIfPresent([CoachingAdviceActionInfo].self, forKey: .actions) ?? []
        let motivation = try container.decodeIfPresent(String.self, forKey: .motivation) ?? ""
        let safetyNote = try container.decodeIfPresent(String.self, forKey: .safetyNote) ?? ""
        let advice = try container.decodeIfPresent(String.self, forKey: .advice)
            ?? Self.buildLegacyAdvice(
                headline: headline,
                summary: summary,
                sections: sections,
                actions: actions,
                motivation: motivation,
                safetyNote: safetyNote
            )

        self.init(
            advice: advice,
            timestamp: try container.decode(Date.self, forKey: .timestamp),
            schemaVersion: schemaVersion,
            headline: headline,
            summary: summary,
            sections: sections,
            actions: actions,
            motivation: motivation,
            safetyNote: safetyNote
        )
    }

    private static func buildLegacyAdvice(
        headline: String,
        summary: String,
        sections: [CoachingAdviceSectionInfo],
        actions: [CoachingAdviceActionInfo],
        motivation: String,
        safetyNote: String
    ) -> String {
        var lines: [String] = []

        if !headline.isEmpty {
            lines.append("**\(headline)**")
        }

        if !summary.isEmpty {
            lines.append(summary)
        }

        for section in sections {
            lines.append("")
            lines.append("**\(section.title)**")
            for item in section.items {
                lines.append("• \(item)")
            }
        }

        if !actions.isEmpty {
            lines.append("")
            lines.append("**Actions**")
            for action in actions {
                lines.append("• \(action.label)")
            }
        }

        if !motivation.isEmpty {
            lines.append("")
            lines.append("*\(motivation)*")
        }

        if !safetyNote.isEmpty {
            lines.append(safetyNote)
        }

        return lines.joined(separator: "\n").trimmingCharacters(in: .whitespacesAndNewlines)
    }
}

