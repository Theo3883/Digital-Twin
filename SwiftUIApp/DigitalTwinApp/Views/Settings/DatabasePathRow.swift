import SwiftUI
import UIKit

struct DatabasePathRow: View {
    let path: String

    var body: some View {
        Section("Developer") {
            VStack(alignment: .leading, spacing: 8) {
                Text("[DB PATH]")
                    .font(.caption.weight(.medium))
                    .foregroundColor(.secondary)

                Text(path)
                    .font(.system(size: 12, design: .monospaced))
                    .foregroundColor(.primary)
                    .textSelection(.enabled)

                Button("Copy") {
                    UIPasteboard.general.string = path
                }
                .font(.caption.weight(.medium))
            }
            .padding(.vertical, 6)
        }
    }
}

