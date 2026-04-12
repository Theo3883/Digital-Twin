import PDFKit
import SwiftUI

private struct DocumentPdfPreview: UIViewRepresentable {
    let data: Data

    func makeUIView(context: Context) -> PDFView {
        let v = PDFView()
        v.autoScales = true
        v.displayMode = .singlePageContinuous
        v.document = PDFDocument(data: data)
        return v
    }

    func updateUIView(_ uiView: PDFView, context: Context) {
        uiView.document = PDFDocument(data: data)
    }
}

struct MedicalDocumentDetailView: View {
    let document: OcrDocumentInfo
    let repository: OcrRepository
    @StateObject private var keychain = VaultKeychainService()
    @State private var decryptedData: Data?
    @State private var previewError: String?
    @State private var isLoadingPreview = false
    @State private var isDeleting = false
    @State private var deleteError: String?
    @Environment(\.dismiss) private var dismiss

    var body: some View {
        ZStack {
            MeshGradientBackground()

            ScrollView {
                VStack(spacing: 16) {
                    metadataCard

                    if !document.encryptedVaultPath.isEmpty {
                        vaultPreviewSection
                    }

                    actionsSection

                    if let deleteError {
                        Text(deleteError)
                            .font(.caption)
                            .foregroundColor(LiquidGlass.redCritical)
                    }

                    Spacer(minLength: 100)
                }
                .padding(16)
            }
        }
        .pageEnterAnimation()
        .navigationTitle(document.displayType)
        .navigationBarTitleDisplayMode(.inline)
    }

    private var metadataCard: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack(spacing: 10) {
                Image(systemName: document.typeIcon)
                    .font(.title2)
                    .foregroundColor(LiquidGlass.tealPrimary)

                VStack(alignment: .leading, spacing: 2) {
                    Text(document.displayType)
                        .font(.system(size: 15, weight: .medium))
                        .foregroundColor(.white)
                        .lineLimit(2)

                    if let date = document.createdAt {
                        Text(date.formatted(date: .abbreviated, time: .shortened))
                            .font(.caption)
                            .foregroundColor(.white.opacity(0.45))
                    }
                }

                Spacer()

                if document.isSynced {
                    Image(systemName: "checkmark.icloud.fill")
                        .font(.caption)
                        .foregroundColor(LiquidGlass.greenPositive.opacity(0.6))
                }
            }

            HStack(spacing: 12) {
                metadataChip(icon: "number", label: "Ref \(document.shortDocRef)")
                metadataChip(icon: "number.square", label: "Hash \(document.shortContentHash)")
            }

            HStack(spacing: 12) {
                metadataChip(icon: "tag.fill", label: document.documentType)
                metadataChip(icon: "doc.fill", label: document.mimeType.components(separatedBy: "/").last ?? document.mimeType)
                metadataChip(icon: "doc.on.doc", label: "\(document.pageCount) page\(document.pageCount == 1 ? "" : "s")")
                metadataChip(icon: "calendar", label: document.scannedAt.formatted(date: .numeric, time: .omitted))
            }
        }
        .glassCard()
    }

    private func metadataChip(icon: String, label: String) -> some View {
        HStack(spacing: 4) {
            Image(systemName: icon)
                .font(.system(size: 10))
            Text(label)
                .font(.system(size: 10, weight: .medium))
                .lineLimit(1)
        }
        .foregroundColor(.white.opacity(0.5))
        .padding(.horizontal, 8)
        .padding(.vertical, 4)
        .background(RoundedRectangle(cornerRadius: LiquidGlass.radiusChip).fill(.white.opacity(0.05)))
    }

    private var vaultPreviewSection: some View {
        VStack(alignment: .leading, spacing: 10) {
            Text("Encrypted document")
                .font(.subheadline.weight(.medium))
                .foregroundColor(.white.opacity(0.65))

            if let data = decryptedData {
                if document.mimeType.lowercased() == "application/pdf" {
                    DocumentPdfPreview(data: data)
                        .frame(minHeight: 360)
                        .clipShape(RoundedRectangle(cornerRadius: LiquidGlass.radiusChip))
                } else if let ui = UIImage(data: data) {
                    Image(uiImage: ui)
                        .resizable()
                        .scaledToFit()
                        .frame(maxHeight: 400)
                        .clipShape(RoundedRectangle(cornerRadius: LiquidGlass.radiusChip))
                } else {
                    Text("Preview not available for this file type.")
                        .font(.caption)
                        .foregroundColor(.white.opacity(0.5))
                }
            } else {
                Button {
                    Task { await loadDecryptedPreview() }
                } label: {
                    HStack {
                        if isLoadingPreview {
                            ProgressView()
                                .tint(.white)
                        } else {
                            Image(systemName: "lock.open.fill")
                            Text("Unlock & open")
                                .font(.subheadline.weight(.medium))
                        }
                    }
                    .foregroundColor(.white)
                    .frame(maxWidth: .infinity)
                    .padding(.vertical, 12)
                }
                .disabled(isLoadingPreview)
                .glassPill(tint: LiquidGlass.tealPrimary.opacity(0.2))
            }

            if let previewError {
                Text(previewError)
                    .font(.caption)
                    .foregroundColor(LiquidGlass.amberWarning)
            }
        }
        .glassCard()
    }

    private func loadDecryptedPreview() async {
        isLoadingPreview = true
        previewError = nil
        defer { isLoadingPreview = false }

        guard let masterKeyB64 = await keychain.retrieveMasterKey(reason: "Unlock this medical document") else {
            previewError = "Authentication failed or was cancelled."
            return
        }

        let unlock = await repository.vaultUnlock(masterKeyBase64: masterKeyB64)
        guard unlock?.success == true else {
            previewError = unlock?.error ?? "Could not unlock vault."
            return
        }

        guard let b64 = await repository.vaultRetrieveDocument(documentId: document.id.uuidString),
              let data = Data(base64Encoded: b64, options: .ignoreUnknownCharacters) else {
            previewError = "Could not decrypt document."
            return
        }

        decryptedData = data
    }

    private var actionsSection: some View {
        Button {
            Task {
                isDeleting = true
                deleteError = nil
                let result = await repository.vaultDeleteDocument(documentId: document.id.uuidString)
                isDeleting = false
                if result?.success == true {
                    dismiss()
                } else {
                    deleteError = result?.error ?? "Failed to delete document."
                }
            }
        } label: {
            HStack(spacing: 6) {
                if isDeleting {
                    ProgressView()
                        .tint(LiquidGlass.redCritical)
                } else {
                    Image(systemName: "trash")
                        .font(.caption)
                }
                Text("Delete from Vault")
                    .font(.caption.weight(.medium))
            }
            .foregroundColor(LiquidGlass.redCritical)
            .frame(maxWidth: .infinity)
            .padding(.vertical, 10)
        }
        .disabled(isDeleting)
        .glassPill(tint: LiquidGlass.redCritical.opacity(0.1))
    }
}
