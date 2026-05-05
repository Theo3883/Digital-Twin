import Foundation

@MainActor
final class OcrDocumentsViewModel: ObservableObject {
    @Published private(set) var documents: [OcrDocumentInfo] = []

    private let repository: OcrRepository

    init(repository: OcrRepository) {
        self.repository = repository
    }

    func load() async {
        documents = await repository.loadDocuments()
    }
}

