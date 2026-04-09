import Foundation

@MainActor
final class MedicalAssistantViewModel: ObservableObject {
    @Published private(set) var messages: [ChatMessageInfo] = []
    @Published var messageText: String = ""
    @Published private(set) var isSending: Bool = false

    private let loadHistory: LoadChatHistoryUseCase
    private let sendMessageUseCase: SendChatMessageUseCase
    private let clearUseCase: ClearChatHistoryUseCase

    init(loadHistory: LoadChatHistoryUseCase, sendMessage: SendChatMessageUseCase, clear: ClearChatHistoryUseCase) {
        self.loadHistory = loadHistory
        self.sendMessageUseCase = sendMessage
        self.clearUseCase = clear
    }

    func onAppear() async {
        messages = await loadHistory()
    }

    func clearChat() {
        Task {
            let _ = await clearUseCase()
            messages = await loadHistory()
        }
    }

    func sendMessage() {
        let text = messageText.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !text.isEmpty, !isSending else { return }

        messageText = ""
        isSending = true

        Task {
            let _ = await sendMessageUseCase(text)
            messages = await loadHistory()
            isSending = false
        }
    }
}

