import Foundation

struct ChatMessageInfo: Codable, Identifiable {
    let id: UUID
    let content: String
    let isUser: Bool
    let timestamp: Date
}

