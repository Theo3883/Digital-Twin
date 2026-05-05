import SwiftUI

struct SanitizedPreviewPanelView: View {
    let sanitizedText: String

    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            Text("Sanitized preview")
                .font(.caption.weight(.semibold))
                .foregroundColor(.white.opacity(0.55))
            ScrollView(showsIndicators: false) {
                Text(sanitizedText)
                    .font(.caption)
                    .foregroundColor(.white.opacity(0.85))
                    .frame(maxWidth: .infinity, alignment: .leading)
            }
            .frame(maxHeight: 160)
        }
        .padding(12)
        .glassCard()
    }
}
