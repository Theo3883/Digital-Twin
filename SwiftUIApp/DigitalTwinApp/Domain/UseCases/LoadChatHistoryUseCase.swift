import Foundation

struct LoadChatHistoryUseCase: Sendable {
    private let repository: ChatRepository

    init(repository: ChatRepository) {
        self.repository = repository
    }

    func callAsFunction() async -> [ChatMessageInfo] {
        await repository.loadHistory()
    }
}

