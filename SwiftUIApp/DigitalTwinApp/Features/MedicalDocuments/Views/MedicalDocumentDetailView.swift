import PDFKit
import SwiftUI
import UIKit

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

private struct ZoomableImagePreview: UIViewRepresentable {
    let image: UIImage

    func makeCoordinator() -> Coordinator {
        Coordinator()
    }

    func makeUIView(context: Context) -> UIScrollView {
        let scrollView = UIScrollView()
        scrollView.backgroundColor = .black
        scrollView.minimumZoomScale = 1
        scrollView.maximumZoomScale = 8
        scrollView.bouncesZoom = true
        scrollView.showsVerticalScrollIndicator = false
        scrollView.showsHorizontalScrollIndicator = false
        scrollView.delegate = context.coordinator

        let imageView = UIImageView(image: image)
        imageView.contentMode = .scaleAspectFit
        imageView.frame = scrollView.bounds
        imageView.autoresizingMask = [.flexibleWidth, .flexibleHeight]
        scrollView.addSubview(imageView)

        context.coordinator.imageView = imageView
        return scrollView
    }

    func updateUIView(_ scrollView: UIScrollView, context: Context) {
        context.coordinator.imageView?.image = image
    }

    final class Coordinator: NSObject, UIScrollViewDelegate {
        weak var imageView: UIImageView?

        func viewForZooming(in scrollView: UIScrollView) -> UIView? {
            imageView
        }
    }
}

private struct FullScreenDocumentViewer: View {
    let data: Data
    let mimeType: String
    let onClose: () -> Void

    var body: some View {
        ZStack(alignment: .topTrailing) {
            Color.black.ignoresSafeArea()

            Group {
                if mimeType.lowercased() == "application/pdf" {
                    DocumentPdfPreview(data: data)
                } else if let image = UIImage(data: data) {
                    ZoomableImagePreview(image: image)
                } else {
                    Text("Preview not available for this file type.")
                        .font(.subheadline)
                        .foregroundColor(.white.opacity(0.8))
                }
            }
            .ignoresSafeArea(edges: .bottom)

            VStack {
                HStack {
                    Spacer()
                    Button(action: onClose) {
                        Image(systemName: "xmark.circle.fill")
                            .font(.system(size: 30, weight: .semibold))
                            .foregroundColor(.white.opacity(0.95))
                            .shadow(radius: 8)
                    }
                }
                .padding(.top, 12)
                .padding(.horizontal, 16)

                Spacer()

                Text("Pinch to zoom")
                    .font(.caption.weight(.medium))
                    .foregroundColor(.white.opacity(0.75))
                    .padding(.horizontal, 12)
                    .padding(.vertical, 6)
                    .background(.black.opacity(0.45))
                    .clipShape(Capsule())
                    .padding(.bottom, 18)
            }
        }
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
    @State private var isFullScreenPreviewPresented = false
    @Environment(\.dismiss) private var dismiss

    var body: some View {
        ZStack {
            MeshGradientBackground()

            ScrollView(showsIndicators: false) {
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
        .fullScreenCover(isPresented: $isFullScreenPreviewPresented) {
            if let data = decryptedData {
                FullScreenDocumentViewer(data: data, mimeType: document.mimeType) {
                    isFullScreenPreviewPresented = false
                }
            } else {
                Color.black.ignoresSafeArea()
            }
        }
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

            Button {
                if decryptedData == nil {
                    Task { await loadDecryptedPreview(openInFullScreen: true) }
                } else {
                    isFullScreenPreviewPresented = true
                }
            } label: {
                HStack {
                    if isLoadingPreview {
                        ProgressView()
                            .tint(.white)
                    } else {
                        Image(systemName: "arrow.up.left.and.arrow.down.right")
                        Text(decryptedData == nil ? "Unlock & Open" : "Open")
                            .font(.subheadline.weight(.medium))
                    }
                }
                .foregroundColor(.white)
                .frame(maxWidth: .infinity)
                .padding(.vertical, 12)
            }
            .disabled(isLoadingPreview)
            .glassPill(tint: LiquidGlass.tealPrimary.opacity(0.2))

            if decryptedData != nil {
                Text("Opens full screen. Use pinch gestures to zoom in and out.")
                    .font(.caption)
                    .foregroundColor(.white.opacity(0.55))
            }

            if let previewError {
                Text(previewError)
                    .font(.caption)
                    .foregroundColor(LiquidGlass.amberWarning)
            }
        }
        .glassCard()
    }

    private func loadDecryptedPreview(openInFullScreen: Bool) async {
        isLoadingPreview = true
        previewError = nil
        print("[OCR Vault][Detail] Unlock & open requested for docId=\(document.id.uuidString)")
        defer { isLoadingPreview = false }

        guard let masterKeyB64 = await keychain.retrieveMasterKey(reason: "Unlock this medical document") else {
            previewError = "Authentication failed or was cancelled."
            print("[OCR Vault][Detail] retrieveMasterKey returned nil")
            return
        }

        let unlock = await repository.vaultUnlock(masterKeyBase64: masterKeyB64)
        guard unlock?.success == true else {
            previewError = unlock?.error ?? "Could not unlock vault."
            print("[OCR Vault][Detail] vaultUnlock failed: \(unlock?.error ?? "nil")")
            return
        }
        print("[OCR Vault][Detail] vaultUnlock success")

        guard let b64 = await repository.vaultRetrieveDocument(documentId: document.id.uuidString),
              let data = Data(base64Encoded: b64, options: .ignoreUnknownCharacters) else {
            previewError = "Could not decrypt document. Check vault debug logs for root cause."
            print("[OCR Vault][Detail] vaultRetrieveDocument failed or returned non-base64 payload")
            return
        }

        decryptedData = data
        print("[OCR Vault][Detail] document preview decrypted, bytes=\(data.count)")

        if openInFullScreen {
            isFullScreenPreviewPresented = true
            print("[OCR Vault][Detail] presenting full-screen viewer")
        }
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
