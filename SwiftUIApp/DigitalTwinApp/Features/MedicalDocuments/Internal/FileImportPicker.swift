import SwiftUI
import UniformTypeIdentifiers

struct ImportedFileResult {
    let data: Data
    let fileName: String
    let fileExtension: String
    let mimeType: String
    let fileSize: Int64
}

struct FileImportPicker: UIViewControllerRepresentable {
    let onFilePicked: (ImportedFileResult) -> Void
    let onCancel: () -> Void
    let onError: (String) -> Void

    private static let maxFileSize: Int64 = 50 * 1024 * 1024
    private static let allowedTypes: [UTType] = [.pdf, .jpeg, .png]
    private static let allowedExtensions: Set<String> = ["pdf", "jpg", "jpeg", "png"]
    private static let magicBytes: [String: [UInt8]] = [
        "pdf": [0x25, 0x50, 0x44, 0x46],
        "jpg": [0xFF, 0xD8, 0xFF],
        "jpeg": [0xFF, 0xD8, 0xFF],
        "png": [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A]
    ]
    private static let mimeTypes: [String: String] = [
        "pdf": "application/pdf",
        "jpg": "image/jpeg",
        "jpeg": "image/jpeg",
        "png": "image/png"
    ]

    func makeUIViewController(context: Context) -> UIDocumentPickerViewController {
        let picker = UIDocumentPickerViewController(forOpeningContentTypes: Self.allowedTypes)
        picker.allowsMultipleSelection = false
        picker.delegate = context.coordinator
        return picker
    }

    func updateUIViewController(_ uiViewController: UIDocumentPickerViewController, context: Context) {}

    func makeCoordinator() -> Coordinator {
        Coordinator(onFilePicked: onFilePicked, onCancel: onCancel, onError: onError)
    }

    @MainActor
    final class Coordinator: NSObject, UIDocumentPickerDelegate {
        let onFilePicked: (ImportedFileResult) -> Void
        let onCancel: () -> Void
        let onError: (String) -> Void

        init(onFilePicked: @escaping (ImportedFileResult) -> Void,
             onCancel: @escaping () -> Void,
             onError: @escaping (String) -> Void) {
            self.onFilePicked = onFilePicked
            self.onCancel = onCancel
            self.onError = onError
        }

        nonisolated func documentPicker(_ controller: UIDocumentPickerViewController, didPickDocumentsAt urls: [URL]) {
            guard let url = urls.first else {
                MainActor.assumeIsolated { onCancel() }
                return
            }

            let accessing = url.startAccessingSecurityScopedResource()
            defer { if accessing { url.stopAccessingSecurityScopedResource() } }

            let ext = url.pathExtension.lowercased()

            guard FileImportPicker.allowedExtensions.contains(ext) else {
                MainActor.assumeIsolated {
                    onError("Extension '\(ext)' is not allowed. Only PDF, JPG, JPEG, PNG are accepted.")
                }
                return
            }

            guard let data = try? Data(contentsOf: url) else {
                MainActor.assumeIsolated { onError("Unable to read the selected file.") }
                return
            }

            guard !data.isEmpty else {
                MainActor.assumeIsolated { onError("File is empty.") }
                return
            }

            let fileSize = Int64(data.count)
            guard fileSize <= FileImportPicker.maxFileSize else {
                MainActor.assumeIsolated { onError("File exceeds maximum allowed size of 50 MB.") }
                return
            }

            if let expectedBytes = FileImportPicker.magicBytes[ext] {
                let headerBytes = Array(data.prefix(expectedBytes.count))
                guard headerBytes.count >= expectedBytes.count,
                      zip(headerBytes, expectedBytes).allSatisfy({ $0 == $1 }) else {
                    MainActor.assumeIsolated {
                        onError("File content does not match its declared extension '\(ext)'.")
                    }
                    return
                }
            }

            let mimeType = FileImportPicker.mimeTypes[ext] ?? "application/octet-stream"
            let fileName = url.lastPathComponent

            let result = ImportedFileResult(
                data: data,
                fileName: fileName,
                fileExtension: ext,
                mimeType: mimeType,
                fileSize: fileSize
            )

            MainActor.assumeIsolated { onFilePicked(result) }
        }

        nonisolated func documentPickerWasCancelled(_ controller: UIDocumentPickerViewController) {
            MainActor.assumeIsolated { onCancel() }
        }
    }
}
