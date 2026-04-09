import SwiftUI

struct MedicalAssistantView: View {
    @EnvironmentObject var engineWrapper: MobileEngineWrapper
    @State private var messageText = ""
    @State private var isSending = false
    @FocusState private var isInputFocused: Bool

    var body: some View {
        NavigationView {
            VStack(spacing: 0) {
                // Chat Messages
                ScrollViewReader { proxy in
                    ScrollView {
                        LazyVStack(spacing: 12) {
                            ForEach(engineWrapper.chatMessages) { message in
                                ChatBubble(message: message)
                                    .id(message.id)
                            }
                        }
                        .padding()
                    }
                    .onChange(of: engineWrapper.chatMessages.count) { _, _ in
                        if let last = engineWrapper.chatMessages.last {
                            withAnimation { proxy.scrollTo(last.id, anchor: .bottom) }
                        }
                    }
                }

                Divider()

                // Input Bar
                HStack(spacing: 12) {
                    TextField("Ask your health assistant...", text: $messageText, axis: .vertical)
                        .lineLimit(1...4)
                        .focused($isInputFocused)

                    Button(action: sendMessage) {
                        Image(systemName: "arrow.up.circle.fill")
                            .font(.title2)
                    }
                    .disabled(messageText.trimmingCharacters(in: .whitespaces).isEmpty || isSending)
                    .buttonStyle(.glassProminent)
                    .glassEffect(.regular.tint(.blue).interactive())
                }
                .glassInputBar()
                .padding(.horizontal)
                .padding(.bottom, 8)
            }
            .navigationTitle("Health Assistant")
            .liquidGlassNavigationStyle()
            .toolbar {
                ToolbarItem(placement: .navigationBarTrailing) {
                    Menu {
                        Button(role: .destructive) {
                            Task { let _ = await engineWrapper.clearChatHistory() }
                        } label: {
                            Label("Clear History", systemImage: "trash")
                        }
                    } label: {
                        Image(systemName: "ellipsis.circle")
                    }
                    .liquidGlassButtonStyle()
                }
            }
            .task {
                await engineWrapper.loadChatHistory()
            }
        }
    }

    private func sendMessage() {
        let text = messageText.trimmingCharacters(in: .whitespaces)
        guard !text.isEmpty else { return }
        messageText = ""
        isSending = true

        Task {
            let _ = await engineWrapper.sendChatMessage(text)
            isSending = false
        }
    }
}

// MARK: - Chat Bubble

struct ChatBubble: View {
    let message: ChatMessageInfo

    var body: some View {
        HStack {
            if message.isUser { Spacer(minLength: 60) }

            VStack(alignment: message.isUser ? .trailing : .leading, spacing: 4) {
                Text(message.content)
                    .font(.body)
                    .foregroundColor(.primary)

                Text(message.timestamp.formatted(date: .omitted, time: .shortened))
                    .font(.caption2)
                    .foregroundColor(.secondary)
            }
            .padding(12)
            .background {
                RoundedRectangle(cornerRadius: 16)
                    .glassEffect(message.isUser ?
                        .regular.tint(LiquidGlass.bluePrimary.opacity(0.3)) :
                        .regular)
            }

            if !message.isUser { Spacer(minLength: 60) }
        }
    }
}
