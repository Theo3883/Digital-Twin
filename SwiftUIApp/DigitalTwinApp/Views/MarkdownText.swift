import Foundation
import SwiftUI

struct MarkdownText: View {
    let text: String

    init(_ text: String) {
        self.text = text
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

        return cleaned.joined(separator: "\n")
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

        return columns.joined(separator: " - ")
    }

    private func plainSegment(_ value: String) -> AttributedString {
        var plain = AttributedString(value)
        plain.foregroundColor = .white
        plain.font = .body
        return plain
    }

    private func boldSegment(_ value: String) -> AttributedString {
        var bold = AttributedString(value)
        bold.foregroundColor = LiquidGlass.tealPrimary
        bold.font = .body.bold()
        return bold
    }

    private func italicSegment(_ value: String) -> AttributedString {
        var italic = AttributedString(value)
        italic.foregroundColor = .white.opacity(0.75)
        italic.font = .body.italic()
        return italic
    }
}

