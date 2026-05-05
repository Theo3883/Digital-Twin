import Foundation

@MainActor
final class EngineChatRepository: ChatRepository {
    private let engine: MobileEngineWrapper

    init(engine: MobileEngineWrapper) {
        self.engine = engine
    }

    func loadHistory() async -> [ChatMessageInfo] {
        await engine.loadChatHistory()
        return engine.chatMessages
    }

    func send(message: String) async -> Bool {
        await engine.sendChatMessage(message)
    }

    func clear() async -> Bool {
        await engine.clearChatHistory()
    }
}

