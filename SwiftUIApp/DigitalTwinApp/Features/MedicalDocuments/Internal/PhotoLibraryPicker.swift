import PhotosUI
import SwiftUI

struct PhotoLibraryPicker: UIViewControllerRepresentable {
    let onImagePicked: (UIImage, Data) -> Void
    let onCancel: () -> Void

    func makeUIViewController(context: Context) -> PHPickerViewController {
        var config = PHPickerConfiguration()
        config.selectionLimit = 1
        config.filter = .images
        let picker = PHPickerViewController(configuration: config)
        picker.delegate = context.coordinator
        return picker
    }

    func updateUIViewController(_ uiViewController: PHPickerViewController, context: Context) {}

    func makeCoordinator() -> Coordinator {
        Coordinator(onImagePicked: onImagePicked, onCancel: onCancel)
    }

    @MainActor
    final class Coordinator: NSObject, PHPickerViewControllerDelegate, @unchecked Sendable {
        let onImagePicked: (UIImage, Data) -> Void
        let onCancel: () -> Void

        init(onImagePicked: @escaping (UIImage, Data) -> Void, onCancel: @escaping () -> Void) {
            self.onImagePicked = onImagePicked
            self.onCancel = onCancel
        }

        nonisolated func picker(_ picker: PHPickerViewController, didFinishPicking results: [PHPickerResult]) {
            guard let provider = results.first?.itemProvider, provider.canLoadObject(ofClass: UIImage.self) else {
                Task { @MainActor in
                    picker.dismiss(animated: true)
                    self.onCancel()
                }
                return
            }

            provider.loadObject(ofClass: UIImage.self) { reading, _ in
                let result: (UIImage, Data)? = {
                    guard let image = reading as? UIImage,
                          let jpegData = image.jpegData(compressionQuality: 0.92) else { return nil }
                    return (image, jpegData)
                }()

                Task { @MainActor in
                    picker.dismiss(animated: true)
                    if let (image, data) = result {
                        self.onImagePicked(image, data)
                    } else {
                        self.onCancel()
                    }
                }
            }
        }
    }
}
