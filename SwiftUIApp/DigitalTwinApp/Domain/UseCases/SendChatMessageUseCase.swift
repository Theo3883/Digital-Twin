import Foundation

struct SendChatMessageUseCase: Sendable {
    private let repository: ChatRepository

    init(repository: ChatRepository) {
        self.repository = repository
    }

    func callAsFunction(_ message: String) async -> Bool {
        await repository.send(message: message)
    }
}

