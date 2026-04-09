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
        var result = AttributedString()
        var remaining = text[...]

        while !remaining.isEmpty {
            // Bold: **text**
            if let boldRange = remaining.range(of: #"\*\*(.+?)\*\*"#, options: .regularExpression) {
                // Add text before bold
                let before = remaining[remaining.startIndex..<boldRange.lowerBound]
                if !before.isEmpty {
                    var plain = AttributedString(String(before))
                    plain.foregroundColor = .white
                    plain.font = .body
                    result.append(plain)
                }
                // Extract bold content (drop ** markers)
                let matched = String(remaining[boldRange])
                let content = String(matched.dropFirst(2).dropLast(2))
                var bold = AttributedString(content)
                bold.foregroundColor = LiquidGlass.tealPrimary
                bold.font = .body.bold()
                result.append(bold)
                remaining = remaining[boldRange.upperBound...]
            }
            // Italic: *text*
            else if let italicRange = remaining.range(of: #"\*(.+?)\*"#, options: .regularExpression) {
                let before = remaining[remaining.startIndex..<italicRange.lowerBound]
                if !before.isEmpty {
                    var plain = AttributedString(String(before))
                    plain.foregroundColor = .white
                    plain.font = .body
                    result.append(plain)
                }
                let matched = String(remaining[italicRange])
                let content = String(matched.dropFirst(1).dropLast(1))
                var italic = AttributedString(content)
                italic.foregroundColor = .white.opacity(0.75)
                italic.font = .body.italic()
                result.append(italic)
                remaining = remaining[italicRange.upperBound...]
            } else {
                // No more patterns — append rest
                var plain = AttributedString(String(remaining))
                plain.foregroundColor = .white
                plain.font = .body
                result.append(plain)
                break
            }
        }
        return result
    }
}

