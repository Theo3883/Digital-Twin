import SwiftUI

/// Mirrors `PostureRow.razor`.
struct PostureRowView: View {
    let label: String
    let isOk: Bool
    var isOptional: Bool = false
    let systemImage: String

    var body: some View {
        HStack(spacing: 10) {
            Image(systemName: systemImage)
                .font(.system(size: 15))
                .foregroundColor(.white.opacity(0.5))
                .frame(width: 22)

            Text(label)
                .font(.subheadline)
                .foregroundColor(.white.opacity(0.9))

            if isOptional {
                Image(systemName: "info.circle")
                    .font(.caption)
                    .foregroundColor(.white.opacity(0.35))
            }

            Spacer()

            Image(systemName: isOk ? "checkmark.circle.fill" : "xmark.circle.fill")
                .foregroundColor(isOk ? LiquidGlass.greenPositive : LiquidGlass.redCritical.opacity(0.9))
        }
    }
}
