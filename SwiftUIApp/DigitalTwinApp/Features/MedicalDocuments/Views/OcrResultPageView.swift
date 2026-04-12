import SwiftUI

/// Mirrors [`OcrResultPage.razor`](DigitalTwin.OCR/Components/Pages/OcrResultPage.razor).
struct OcrResultPageView: View {
    @ObservedObject var controller: OcrSessionController
    let repository: OcrRepository
    let onDone: () -> Void

    private var sanitized: String {
        controller.processingResult?.sanitizedText ?? ""
    }

    var body: some View {
        VStack(spacing: 16) {
            HStack {
                Image(systemName: "checkmark.circle.fill")
                    .foregroundColor(LiquidGlass.greenPositive)
                Text("Document Processed")
                    .font(.headline)
                    .foregroundColor(.white)
                Spacer()
            }
            .padding(16)
            .glassCard()

            if let doc = controller.lastSavedDocument {
                VStack(alignment: .leading, spacing: 6) {
                    row("Type", doc.mimeType)
                    row("Pages", "\(doc.pageCount)")
                    row("Scanned", doc.scannedAt.formatted(date: .abbreviated, time: .shortened))
                    row("Integrity", String(doc.sha256OfNormalized.prefix(16)) + "…")
                }
                .font(.caption)
                .foregroundColor(.white.opacity(0.75))
                .padding(16)
                .glassCard()
            }

            if !sanitized.isEmpty {
                SanitizedPreviewPanelView(sanitizedText: sanitized)
            }

            Text("Sync runs automatically in the background.")
                .font(.caption)
                .foregroundColor(.white.opacity(0.45))
                .multilineTextAlignment(.center)

            if let doc = controller.lastSavedDocument {
                Button(role: .destructive) {
                    Task {
                        _ = await repository.vaultDeleteDocument(documentId: doc.id.uuidString)
                    }
                } label: {
                    HStack {
                        Image(systemName: "trash")
                        Text("Delete")
                    }
                    .font(.subheadline.weight(.medium))
                    .frame(maxWidth: .infinity)
                    .padding(.vertical, 10)
                }
                .glassPill(tint: LiquidGlass.redCritical.opacity(0.2))
            }

            Button(action: onDone) {
                Text("Done")
                    .font(.headline)
                    .foregroundColor(.white)
                    .frame(maxWidth: .infinity)
                    .padding(.vertical, 14)
            }
            .glassPill(tint: LiquidGlass.tealPrimary)
        }
    }

    private func row(_ k: String, _ v: String) -> some View {
        HStack(alignment: .top) {
            Text(k + ":")
                .foregroundColor(.white.opacity(0.45))
            Text(v)
                .foregroundColor(.white.opacity(0.9))
            Spacer()
        }
    }
}
