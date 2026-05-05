import Foundation

struct ClearChatHistoryUseCase: Sendable {
    private let repository: ChatRepository

    init(repository: ChatRepository) {
        self.repository = repository
    }

    func callAsFunction() async -> Bool {
        await repository.clear()
    }
}

