import SwiftUI

struct OcrDocumentView: View {
    @EnvironmentObject var engineWrapper: MobileEngineWrapper
    @StateObject private var coordinator = OcrSessionCoordinator()
    @State private var showingScanner = false

    var body: some View {
        NavigationView {
            ScrollView {
                VStack(spacing: 16) {
                    // Action Buttons
                    HStack(spacing: 12) {
                        Button {
                            coordinator.startScan()
                        } label: {
                            Label("Scan Document", systemImage: "doc.text.viewfinder")
                                .frame(maxWidth: .infinity)
                        }
                        .buttonStyle(.glass)
                        .frame(height: 50)

                        Button {
                            coordinator.showPhotoPicker = true
                        } label: {
                            Label("Import Photo", systemImage: "photo.on.rectangle")
                                .frame(maxWidth: .infinity)
                        }
                        .buttonStyle(.glass)
                        .frame(height: 50)
                    }
                    
                    // Processing indicator
                    if coordinator.isProcessing {
                        HStack(spacing: 8) {
                            ProgressView()
                            Text("Processing document…")
                                .font(.subheadline).foregroundColor(.secondary)
                        }
                        .glassCard(tint: .blue)
                    }
                    
                    // Error banner
                    if let error = coordinator.error {
                        HStack {
                            Image(systemName: "exclamationmark.triangle.fill")
                                .foregroundColor(.orange)
                            Text(error)
                                .font(.caption)
                        }
                        .glassBanner(tint: .orange)
                    }
                    
                    // Processing result
                    if let result = coordinator.processingResult {
                        OcrResultCard(result: result)
                    }

                    // Document List
                    if engineWrapper.ocrDocuments.isEmpty {
                        EmptyDocumentsView()
                    } else {
                        VStack(alignment: .leading, spacing: 12) {
                            Text("Scanned Documents")
                                .glassSectionHeader()

                            ForEach(engineWrapper.ocrDocuments) { doc in
                                OcrDocumentCard(document: doc)
                            }
                        }
                    }

                    Spacer(minLength: 100)
                }
                .padding()
            }
            .navigationTitle("Documents")
            .liquidGlassNavigationStyle()
            .task {
                await engineWrapper.loadOcrDocuments()
            }
            .refreshable {
                await engineWrapper.loadOcrDocuments()
            }
            .sheet(isPresented: $coordinator.showScanner) {
                DocumentScannerView(
                    onScanComplete: { images in
                        coordinator.handleScannedImages(images, engine: engineWrapper)
                    },
                    onCancel: {
                        coordinator.handleScannerCancel()
                    }
                )
            }
        }
    }
}

// MARK: - OCR Result Card

struct OcrResultCard: View {
    let result: OcrProcessingResult

    var body: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack {
                Image(systemName: "checkmark.circle.fill")
                    .foregroundColor(.green)
                Text("Document Processed")
                    .font(.headline)
                Spacer()
                Text(result.documentType)
                    .glassChip(tint: .blue)
            }

            if let identity = result.identity {
                if let name = identity.extractedName {
                    Label(name, systemImage: "person.fill")
                        .font(.subheadline)
                }
                if let cnp = identity.extractedCnp {
                    Label("CNP: \(cnp.prefix(4))****", systemImage: "number")
                        .font(.caption).foregroundColor(.secondary)
                }
            }

            if let validation = result.validation {
                HStack(spacing: 6) {
                    Image(systemName: validation.isValid ? "checkmark.shield.fill" : "xmark.shield.fill")
                        .foregroundColor(validation.isValid ? .green : .red)
                    Text(validation.isValid ? "Identity verified" : (validation.reason ?? "Identity mismatch"))
                        .font(.caption)
                }
            }

            if !result.historyItems.isEmpty {
                Text("\(result.historyItems.count) medication(s) extracted")
                    .font(.caption).foregroundColor(.secondary)
            }
        }
        .glassCard(tint: .green)
    }
}

// MARK: - Document Card

struct OcrDocumentCard: View {
    let document: OcrDocumentInfo

    var body: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack {
                Image(systemName: document.typeIcon)
                    .font(.title2)
                    .foregroundColor(.blue)
                    .frame(width: 40, height: 40)
                    .background {
                        Circle().glassEffect(.regular.tint(.blue.opacity(0.2)))
                    }

                VStack(alignment: .leading, spacing: 2) {
                    Text(document.opaqueInternalName ?? "Document")
                        .font(.headline)
                        .lineLimit(1)
                    Text(document.scannedAt.formatted(date: .abbreviated, time: .shortened))
                        .font(.caption).foregroundColor(.secondary)
                }

                Spacer()

                VStack(alignment: .trailing, spacing: 4) {
                    Text("\(document.pageCount) pg")
                        .font(.caption).foregroundColor(.secondary)
                    Image(systemName: document.isSynced ? "checkmark.icloud.fill" : "icloud.and.arrow.up")
                        .font(.caption)
                        .foregroundColor(document.isSynced ? .green : .orange)
                }
            }

            if let preview = document.sanitizedOcrPreview, !preview.isEmpty {
                Text(preview)
                    .font(.caption)
                    .foregroundColor(.secondary)
                    .lineLimit(3)
                    .padding(.top, 4)
            }
        }
        .glassCard()
    }
}

// MARK: - Empty State

struct EmptyDocumentsView: View {
    var body: some View {
        VStack(spacing: 16) {
            Image(systemName: "doc.text.fill")
                .font(.system(size: 50)).foregroundColor(.secondary)
            Text("No Documents")
                .font(.title3).fontWeight(.semibold)
            Text("Scan or import medical documents to extract and organize your health records.")
                .font(.subheadline).foregroundColor(.secondary).multilineTextAlignment(.center)
        }
        .frame(maxWidth: .infinity)
        .padding(.vertical, 60)
    }
}
