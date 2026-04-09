import SwiftUI

struct OcrDocumentCard: View {
    let document: OcrDocumentInfo
    let isUnlocked: Bool
    let onUnlock: () -> Void

    private var mimeLabel: String {
        if let name = document.opaqueInternalName?.lowercased() {
            if name.hasSuffix(".pdf") { return "PDF" }
            if name.hasSuffix(".jpg") || name.hasSuffix(".jpeg") { return "JPG" }
            if name.hasSuffix(".png") { return "PNG" }
        }
        return "IMG"
    }

    private var mimeColor: Color {
        switch mimeLabel {
        case "PDF": return LiquidGlass.redCritical
        case "JPG", "JPEG": return LiquidGlass.amberWarning
        case "PNG": return .blue
        default: return .gray
        }
    }

    var body: some View {
        HStack(spacing: 12) {
            Text(mimeLabel)
                .font(.system(size: 10, weight: .bold))
                .foregroundColor(mimeColor)
                .frame(width: 40, height: 40)
                .background {
                    RoundedRectangle(cornerRadius: 10)
                        .fill(mimeColor.opacity(0.15))
                }

            VStack(alignment: .leading, spacing: 4) {
                Text(document.opaqueInternalName ?? "Document")
                    .font(.system(size: 14, weight: .medium))
                    .foregroundColor(.white)
                    .lineLimit(1)
                HStack(spacing: 8) {
                    Text(document.scannedAt.formatted(date: .abbreviated, time: .shortened))
                        .font(.caption2)
                        .foregroundColor(.white.opacity(0.4))
                    Text("·")
                        .foregroundColor(.white.opacity(0.2))
                    Text("\(document.pageCount) pg")
                        .font(.caption2)
                        .foregroundColor(.white.opacity(0.4))
                }
            }

            Spacer()

            if isUnlocked {
                Image(systemName: "lock.open.fill")
                    .font(.caption)
                    .foregroundColor(LiquidGlass.greenPositive)
            } else {
                Button(action: onUnlock) {
                    HStack(spacing: 4) {
                        Image(systemName: "faceid")
                            .font(.system(size: 12))
                        Text("Unlock")
                            .font(.caption2.weight(.medium))
                    }
                    .foregroundColor(LiquidGlass.tealPrimary)
                    .padding(.horizontal, 10)
                    .padding(.vertical, 6)
                    .background {
                        RoundedRectangle(cornerRadius: LiquidGlass.radiusChip)
                            .stroke(LiquidGlass.tealPrimary.opacity(0.4), lineWidth: 1)
                    }
                }
            }
        }
        .glassCard()
    }
}

