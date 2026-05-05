import SwiftUI

struct OcrResultCard: View {
    let result: OcrProcessingResult

    var body: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack {
                Image(systemName: "checkmark.circle.fill")
                    .foregroundColor(LiquidGlass.greenPositive)
                Text("Document Processed")
                    .font(.subheadline.weight(.semibold))
                    .foregroundColor(.white)
                Spacer()
                Text(result.documentType)
                    .font(.system(size: 10, weight: .medium))
                    .foregroundColor(LiquidGlass.tealPrimary)
                    .padding(.horizontal, 8)
                    .padding(.vertical, 3)
                    .background(
                        RoundedRectangle(cornerRadius: LiquidGlass.radiusChip)
                            .fill(LiquidGlass.tealPrimary.opacity(0.15))
                    )
            }

            if let identity = result.identity {
                if let name = identity.extractedName {
                    Label(name, systemImage: "person.fill")
                        .font(.caption)
                        .foregroundColor(.white.opacity(0.7))
                }
                if let cnp = identity.extractedCnp {
                    Label("CNP: \(cnp.prefix(4))****", systemImage: "number")
                        .font(.caption2)
                        .foregroundColor(.white.opacity(0.4))
                }
            }

            if let validation = result.validation {
                HStack(spacing: 6) {
                    Image(systemName: validation.isValid ? "checkmark.shield.fill" : "xmark.shield.fill")
                        .foregroundColor(validation.isValid ? LiquidGlass.greenPositive : LiquidGlass.redCritical)
                    Text(validation.isValid ? "Identity verified" : (validation.reason ?? "Identity mismatch"))
                        .font(.caption)
                        .foregroundColor(.white.opacity(0.65))
                }
            }

            if !result.historyItems.isEmpty {
                Text("\(result.historyItems.count) medication(s) extracted")
                    .font(.caption)
                    .foregroundColor(.white.opacity(0.4))
            }
        }
        .glassCard()
    }
}
