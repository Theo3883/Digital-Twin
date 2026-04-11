import Foundation
import SwiftUI

struct MarkdownText: View {
    let text: String
    let baseFont: Font
    let baseColor: Color
    let boldColor: Color
    let italicColor: Color

    init(
        _ text: String,
        baseFont: Font = .body,
        baseColor: Color = .white,
        boldColor: Color = LiquidGlass.tealPrimary,
        italicColor: Color = .white.opacity(0.75)
    ) {
        self.text = text
        self.baseFont = baseFont
        self.baseColor = baseColor
        self.boldColor = boldColor
        self.italicColor = italicColor
    }

    var body: some View {
        Text(attributedString)
    }

    private var attributedString: AttributedString {
        let source = normalize(text)
        let pattern = #"\*\*(.+?)\*\*|\*(.+?)\*"#
        guard let regex = try? NSRegularExpression(pattern: pattern) else {
            return plainSegment(source)
        }

        let nsSource = source as NSString
        let fullRange = NSRange(location: 0, length: nsSource.length)
        let matches = regex.matches(in: source, options: [], range: fullRange)

        if matches.isEmpty {
            return plainSegment(source)
        }

        var result = AttributedString()
        var currentLocation = 0

        for match in matches {
            if match.range.location > currentLocation {
                let plainRange = NSRange(location: currentLocation, length: match.range.location - currentLocation)
                let plainText = nsSource.substring(with: plainRange)
                result.append(plainSegment(plainText))
            }

            if let boldRange = Range(match.range(at: 1), in: source), !boldRange.isEmpty {
                result.append(boldSegment(String(source[boldRange])))
            } else if let italicRange = Range(match.range(at: 2), in: source), !italicRange.isEmpty {
                result.append(italicSegment(String(source[italicRange])))
            }

            currentLocation = match.range.location + match.range.length
        }

        if currentLocation < nsSource.length {
            let tailRange = NSRange(location: currentLocation, length: nsSource.length - currentLocation)
            result.append(plainSegment(nsSource.substring(with: tailRange)))
        }

        // MAUI converts newlines to <br /> in HTML rendering.
        // SwiftUI Text preserves newlines natively, which gives equivalent output.
        return result
    }

    private func normalize(_ value: String) -> String {
        let normalizedBreaks = replaceHtmlBreaks(in: value)
        let lines = normalizedBreaks
            .replacingOccurrences(of: "\r\n", with: "\n")
            .components(separatedBy: "\n")

        let cleaned = lines.map { line in
            if isMarkdownTableSeparator(line) {
                return ""
            }

            if let transformed = transformMarkdownTableRow(line) {
                return transformed
            }

            return line
        }

        let joined = cleaned.joined(separator: "\n")
        return enforceSectionSpacing(in: joined)
    }

    private func replaceHtmlBreaks(in value: String) -> String {
        guard let regex = try? NSRegularExpression(pattern: #"(?i)<br\s*/?>"#) else {
            return value
        }

        let fullRange = NSRange(location: 0, length: (value as NSString).length)
        return regex.stringByReplacingMatches(in: value, options: [], range: fullRange, withTemplate: "\n")
    }

    private func isMarkdownTableSeparator(_ line: String) -> Bool {
        let trimmed = line.trimmingCharacters(in: .whitespaces)
        guard !trimmed.isEmpty else {
            return false
        }

        guard let regex = try? NSRegularExpression(pattern: #"^\|?\s*:?-{3,}:?\s*(\|\s*:?-{3,}:?\s*)+\|?$"#) else {
            return false
        }

        let range = NSRange(location: 0, length: (trimmed as NSString).length)
        return regex.firstMatch(in: trimmed, options: [], range: range) != nil
    }

    private func transformMarkdownTableRow(_ line: String) -> String? {
        guard line.contains("|") else {
            return nil
        }

        var row = line.trimmingCharacters(in: .whitespaces)
        guard !row.isEmpty else {
            return nil
        }

        if row.hasPrefix("|") {
            row.removeFirst()
        }
        if row.hasSuffix("|") {
            row.removeLast()
        }

        let columns = row
            .split(separator: "|", omittingEmptySubsequences: false)
            .map { $0.trimmingCharacters(in: .whitespaces) }
            .filter { !$0.isEmpty }

        guard columns.count >= 2 else {
            return nil
        }

        if isGuidanceHeaderRow(columns) {
            return ""
        }

        var sections: [String] = []
        var currentSection = ""

        for column in columns {
            let cleanedColumn = normalizeInlineBullets(in: column)

            if isSectionHeading(cleanedColumn) {
                if !currentSection.isEmpty {
                    sections.append(currentSection)
                }
                currentSection = cleanedColumn
                continue
            }

            if currentSection.isEmpty {
                currentSection = cleanedColumn
                continue
            }

            if cleanedColumn.hasPrefix("•") {
                currentSection += "\n\(cleanedColumn)"
            } else {
                currentSection += " - \(cleanedColumn)"
            }
        }

        if !currentSection.isEmpty {
            sections.append(currentSection)
        }

        return sections.joined(separator: "\n\n")
    }

    private func isGuidanceHeaderRow(_ columns: [String]) -> Bool {
        let normalized = columns.map {
            $0
                .trimmingCharacters(in: .whitespacesAndNewlines)
                .lowercased()
        }

        let joined = normalized.joined(separator: "|")
        return joined.contains("goal")
            && joined.contains("why it matters")
            && joined.contains("practical steps")
            && joined.contains("how to track")
    }

    private func isSectionHeading(_ value: String) -> Bool {
        let trimmed = value.trimmingCharacters(in: .whitespacesAndNewlines)
        guard trimmed.hasPrefix("**") else {
            return false
        }

        let afterOpening = trimmed.index(trimmed.startIndex, offsetBy: 2)
        guard let closingRange = trimmed.range(of: "**", range: afterOpening..<trimmed.endIndex) else {
            return false
        }

        return trimmed[..<closingRange.upperBound].count > 4
    }

    private func normalizeInlineBullets(in value: String) -> String {
        guard let regex = try? NSRegularExpression(pattern: #"\s*-\s*•\s*"#) else {
            return value.trimmingCharacters(in: .whitespacesAndNewlines)
        }

        let fullRange = NSRange(location: 0, length: (value as NSString).length)
        let replaced = regex.stringByReplacingMatches(in: value, options: [], range: fullRange, withTemplate: "\n• ")
        return replaced.trimmingCharacters(in: .whitespacesAndNewlines)
    }

    private func enforceSectionSpacing(in value: String) -> String {
        var spaced = value

        // Handle rows where new bold sections are still inline after parsing.
        spaced = replacingRegex(
            in: spaced,
            pattern: #"\s*\|\s*(\*\*[^*]+\*\*\s*[-:])"#,
            template: "\n\n$1"
        )

        // Ensure each bold heading starts as a new paragraph block.
        spaced = replacingRegex(
            in: spaced,
            pattern: #"([^\n])\s+(\*\*[^*]+\*\*\s*[-:])"#,
            template: "$1\n\n$2"
        )

        spaced = replacingRegex(
            in: spaced,
            pattern: #"\n{3,}"#,
            template: "\n\n"
        )

        return spaced.trimmingCharacters(in: .whitespacesAndNewlines)
    }

    private func replacingRegex(in value: String, pattern: String, template: String) -> String {
        guard let regex = try? NSRegularExpression(pattern: pattern) else {
            return value
        }

        let fullRange = NSRange(location: 0, length: (value as NSString).length)
        return regex.stringByReplacingMatches(in: value, options: [], range: fullRange, withTemplate: template)
    }

    private func plainSegment(_ value: String) -> AttributedString {
        var plain = AttributedString(value)
        plain.foregroundColor = baseColor
        plain.font = baseFont
        return plain
    }

    private func boldSegment(_ value: String) -> AttributedString {
        var bold = AttributedString(value)
        bold.foregroundColor = boldColor
        bold.font = baseFont.bold()
        return bold
    }

    private func italicSegment(_ value: String) -> AttributedString {
        var italic = AttributedString(value)
        italic.foregroundColor = italicColor
        italic.font = baseFont.italic()
        return italic
    }
}

