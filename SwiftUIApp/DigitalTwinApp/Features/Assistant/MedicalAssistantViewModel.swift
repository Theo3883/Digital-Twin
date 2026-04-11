import Foundation

@MainActor
final class MedicalAssistantViewModel: ObservableObject {
    @Published private(set) var messages: [ChatMessageInfo] = []
    @Published var messageText: String = ""
    @Published private(set) var isSending: Bool = false
    @Published private(set) var assistantStatusText: String?

    private let loadHistory: LoadChatHistoryUseCase
    private let sendMessageUseCase: SendChatMessageUseCase
    private let clearUseCase: ClearChatHistoryUseCase
    private var activeSendId: UUID?

    private static let retryHintDelayNanoseconds: UInt64 = 1_200_000_000

    init(loadHistory: LoadChatHistoryUseCase, sendMessage: SendChatMessageUseCase, clear: ClearChatHistoryUseCase) {
        self.loadHistory = loadHistory
        self.sendMessageUseCase = sendMessage
        self.clearUseCase = clear
    }

    func onAppear() async {
        messages = await loadHistory()
        refreshStatusFromLatestMessage()
    }

    func clearChat() {
        Task {
            let _ = await clearUseCase()
            messages = await loadHistory()
            assistantStatusText = nil
        }
    }

    func sendMessage() {
        let text = messageText.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !text.isEmpty, !isSending else { return }

        let optimisticUserMessage = ChatMessageInfo(
            id: UUID(),
            content: text,
            isUser: true,
            timestamp: Date()
        )
        messages.append(optimisticUserMessage)

        let sendId = UUID()
        activeSendId = sendId
        assistantStatusText = nil
        messageText = ""
        isSending = true

        scheduleRetryHint(for: sendId)

        Task {
            let _ = await sendMessageUseCase(text)
            messages = await loadHistory()

            guard activeSendId == sendId else { return }

            isSending = false
            activeSendId = nil
            refreshStatusFromLatestMessage()
        }
    }

    private func scheduleRetryHint(for sendId: UUID) {
        Task {
            try? await Task.sleep(nanoseconds: Self.retryHintDelayNanoseconds)

            guard isSending, activeSendId == sendId else { return }
            assistantStatusText = "Gemini is retrying due to rate limits..."
        }
    }

    private func refreshStatusFromLatestMessage() {
        guard let lastAssistantMessage = messages.last(where: { !$0.isUser }) else {
            if !isSending {
                assistantStatusText = nil
            }
            return
        }

        if isQuotaExhaustedMessage(lastAssistantMessage.content) {
            assistantStatusText = "Gemini quota is exhausted. Please try again later."
        } else if isRateLimitedMessage(lastAssistantMessage.content) {
            assistantStatusText = "Gemini is rate limited. Please try again in a minute."
        } else if !isSending {
            assistantStatusText = nil
        }
    }

    private func isRateLimitedMessage(_ content: String) -> Bool {
        content.localizedCaseInsensitiveContains("rate limited") ||
        content.localizedCaseInsensitiveContains("temporarily unavailable due to rate limits")
    }

    private func isQuotaExhaustedMessage(_ content: String) -> Bool {
        content.localizedCaseInsensitiveContains("quota is exhausted") ||
        content.localizedCaseInsensitiveContains("billing details")
    }
}

