import Foundation

protocol ChatRepository: Sendable {
    func loadHistory() async -> [ChatMessageInfo]
    func send(message: String) async -> Bool
    func clear() async -> Bool
}

