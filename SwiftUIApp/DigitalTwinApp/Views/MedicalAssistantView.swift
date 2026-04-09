import SwiftUI

struct MedicalAssistantView: View {
    @EnvironmentObject var engineWrapper: MobileEngineWrapper
    @State private var messageText = ""
    @State private var isSending = false
    @FocusState private var isInputFocused: Bool

    var body: some View {
        VStack(spacing: 0) {
            // Custom Top Bar
            assistantTopBar
            
            // Chat area
            if engineWrapper.chatMessages.isEmpty && !isSending {
                welcomeScreen
            } else {
                chatMessagesList
            }
            
            // Input Bar
            chatInputBar
        }
        .pageEnterAnimation()
        .task {
            await engineWrapper.loadChatHistory()
        }
    }

    // MARK: - Top Bar

    private var assistantTopBar: some View {
        HStack(spacing: 12) {
            // Avatar
            ZStack {
                Circle()
                    .fill(LinearGradient(
                        colors: [Color(red: 96/255, green: 165/255, blue: 250/255),
                                 Color(red: 168/255, green: 85/255, blue: 247/255)],
                        startPoint: .topLeading, endPoint: .bottomTrailing
                    ))
                    .frame(width: 48, height: 48)
                Image(systemName: "sparkle")
                    .font(.system(size: 20))
                    .foregroundColor(.white)
            }
            
            VStack(alignment: .leading, spacing: 2) {
                Text("MedAssist AI")
                    .font(.system(size: 17, weight: .semibold))
                    .foregroundColor(.white)
                HStack(spacing: 4) {
                    Circle()
                        .fill(LiquidGlass.greenPositive)
                        .frame(width: 6, height: 6)
                    Text("Online")
                        .font(.caption)
                        .foregroundColor(LiquidGlass.greenPositive)
                }
            }
            
            Spacer()
            
            // Clear chat
            Button(action: {
                Task { let _ = await engineWrapper.clearChatHistory() }
            }) {
                Image(systemName: "trash")
                    .font(.system(size: 16))
                    .foregroundColor(.white.opacity(0.5))
            }
            .glassPill()
        }
        .padding(.horizontal, 16)
        .padding(.vertical, 10)
    }

    // MARK: - Welcome Screen

    private var welcomeScreen: some View {
        VStack(spacing: 16) {
            Spacer()
            
            ZStack {
                Circle()
                    .fill(LiquidGlass.tealPrimary.opacity(0.15))
                    .frame(width: 64, height: 64)
                Image(systemName: "sparkle")
                    .font(.system(size: 28))
                    .foregroundColor(LiquidGlass.tealPrimary)
            }
            
            Text("CardioCompanion")
                .font(.title3.weight(.semibold))
                .foregroundColor(.white)
            
            Text("Ask me anything about your heart health, vitals, medications, exercise, or sleep.")
                .font(.subheadline)
                .foregroundColor(.white.opacity(0.65))
                .multilineTextAlignment(.center)
                .padding(.horizontal, 40)
            
            Spacer()
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }

    // MARK: - Chat Messages List

    private var chatMessagesList: some View {
        ScrollViewReader { proxy in
            ScrollView {
                LazyVStack(spacing: 12) {
                    ForEach(engineWrapper.chatMessages) { message in
                        ChatBubble(message: message)
                            .id(message.id)
                    }
                    
                    if isSending {
                        typingIndicator
                    }
                }
                .padding(16)
            }
            .onChange(of: engineWrapper.chatMessages.count) { _, _ in
                if let last = engineWrapper.chatMessages.last {
                    withAnimation { proxy.scrollTo(last.id, anchor: .bottom) }
                }
            }
        }
    }

    // MARK: - Typing Indicator

    private var typingIndicator: some View {
        HStack(spacing: 4) {
            // Bot avatar
            ZStack {
                Circle()
                    .fill(LiquidGlass.tealPrimary.opacity(0.3))
                    .frame(width: 28, height: 28)
                Image(systemName: "sparkle")
                    .font(.system(size: 12))
                    .foregroundColor(LiquidGlass.tealPrimary)
            }
            
            HStack(spacing: 4) {
                ForEach(0..<3, id: \.self) { i in
                    Circle()
                        .fill(.white.opacity(0.5))
                        .frame(width: 6, height: 6)
                        .offset(y: isSending ? -4 : 0)
                        .animation(
                            .easeInOut(duration: 0.5)
                            .repeatForever(autoreverses: true)
                            .delay(Double(i) * 0.15),
                            value: isSending
                        )
                }
            }
            .padding(.horizontal, 16)
            .padding(.vertical, 12)
            .glassEffect(.regular, in: RoundedRectangle(cornerRadius: 16))
            
            Spacer(minLength: 60)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }

    // MARK: - Input Bar

    private var chatInputBar: some View {
        HStack(spacing: 12) {
            TextField("Ask your health assistant...", text: $messageText, axis: .vertical)
                .lineLimit(1...4)
                .focused($isInputFocused)
                .foregroundColor(.white)
                .tint(LiquidGlass.tealPrimary)

            Button(action: sendMessage) {
                ZStack {
                    Circle()
                        .fill(LinearGradient(
                            colors: [LiquidGlass.tealPrimary, LiquidGlass.tealPrimaryDark],
                            startPoint: .topLeading, endPoint: .bottomTrailing
                        ))
                        .frame(width: 44, height: 44)
                    Image(systemName: "arrow.up")
                        .font(.system(size: 16, weight: .bold))
                        .foregroundColor(.white)
                }
            }
            .disabled(messageText.trimmingCharacters(in: .whitespaces).isEmpty || isSending)
            .opacity(messageText.trimmingCharacters(in: .whitespaces).isEmpty ? 0.4 : 1)
        }
        .glassInputBar()
        .padding(.horizontal, 16)
        .padding(.bottom, 8)
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
        HStack(alignment: .top, spacing: 8) {
            if message.isUser {
                Spacer(minLength: 60)
            } else {
                // Bot avatar
                ZStack {
                    Circle()
                        .fill(LiquidGlass.tealPrimary.opacity(0.3))
                        .frame(width: 28, height: 28)
                    Image(systemName: "sparkle")
                        .font(.system(size: 12))
                        .foregroundColor(LiquidGlass.tealPrimary)
                }
            }

            VStack(alignment: message.isUser ? .trailing : .leading, spacing: 4) {
                if message.isUser {
                    Text(message.content)
                        .font(.body)
                        .foregroundColor(.white)
                } else {
                    MarkdownText(message.content)
                }

                Text(message.timestamp.formatted(date: .omitted, time: .shortened))
                    .font(.caption2)
                    .foregroundColor(.white.opacity(0.4))
            }
            .padding(12)
            .background {
                if message.isUser {
                    UnevenRoundedRectangle(
                        topLeadingRadius: 16, bottomLeadingRadius: 16,
                        bottomTrailingRadius: 4, topTrailingRadius: 16
                    )
                    .fill(LiquidGlass.bluePrimary)
                }
            }
            .modifier(ConditionalGlassEffect(
                isEnabled: !message.isUser,
                shape: UnevenRoundedRectangle(
                    topLeadingRadius: 4, bottomLeadingRadius: 16,
                    bottomTrailingRadius: 16, topTrailingRadius: 16
                )
            ))

            if !message.isUser { Spacer(minLength: 60) }
        }
    }
}

// MARK: - Conditional Glass Effect

struct ConditionalGlassEffect<S: InsettableShape>: ViewModifier {
    let isEnabled: Bool
    let shape: S

    func body(content: Content) -> some View {
        if isEnabled {
            content.glassEffect(.regular, in: shape)
        } else {
            content
        }
    }
}

// MARK: - Simple Markdown Text (bold → teal, italic → lighter)

struct MarkdownText: View {
    let text: String

    init(_ text: String) {
        self.text = text
    }

    var body: some View {
        Text(attributedString)
    }

    private var attributedString: AttributedString {
        var result = AttributedString()
        var remaining = text[...]

        while !remaining.isEmpty {
            // Bold: **text**
            if let boldRange = remaining.range(of: #"\*\*(.+?)\*\*"#, options: .regularExpression) {
                // Add text before bold
                let before = remaining[remaining.startIndex..<boldRange.lowerBound]
                if !before.isEmpty {
                    var plain = AttributedString(String(before))
                    plain.foregroundColor = .white
                    plain.font = .body
                    result.append(plain)
                }
                // Extract bold content (drop ** markers)
                let matched = String(remaining[boldRange])
                let content = String(matched.dropFirst(2).dropLast(2))
                var bold = AttributedString(content)
                bold.foregroundColor = LiquidGlass.tealPrimary
                bold.font = .body.bold()
                result.append(bold)
                remaining = remaining[boldRange.upperBound...]
            }
            // Italic: *text*
            else if let italicRange = remaining.range(of: #"\*(.+?)\*"#, options: .regularExpression) {
                let before = remaining[remaining.startIndex..<italicRange.lowerBound]
                if !before.isEmpty {
                    var plain = AttributedString(String(before))
                    plain.foregroundColor = .white
                    plain.font = .body
                    result.append(plain)
                }
                let matched = String(remaining[italicRange])
                let content = String(matched.dropFirst(1).dropLast(1))
                var italic = AttributedString(content)
                italic.foregroundColor = .white.opacity(0.75)
                italic.font = .body.italic()
                result.append(italic)
                remaining = remaining[italicRange.upperBound...]
            } else {
                // No more patterns — append rest
                var plain = AttributedString(String(remaining))
                plain.foregroundColor = .white
                plain.font = .body
                result.append(plain)
                break
            }
        }
        return result
    }
}
