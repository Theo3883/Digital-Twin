import SwiftUI

struct MedicalAssistantView: View {
    @EnvironmentObject var engineWrapper: MobileEngineWrapper
    @Environment(\.horizontalSizeClass) private var horizontalSizeClass
    @Environment(\.dismiss) private var dismiss
    @StateObject private var viewModel: MedicalAssistantViewModel
    @FocusState private var isInputFocused: Bool
    @State private var scrollViewMaxY: CGFloat = 0
    @State private var bottomAnchorMaxY: CGFloat = 0
    @State private var userIsReviewingHistory = false
    @State private var didRunInitialConversationScroll = false
    @State private var pendingAutoScrollWorkItem: DispatchWorkItem?

    private static let chatBottomAnchorId = "medical-assistant-chat-bottom"
    private static let nearBottomThreshold: CGFloat = 120
    private static let autoScrollDelay: TimeInterval = 0.06

    init(viewModel: MedicalAssistantViewModel) {
        _viewModel = StateObject(wrappedValue: viewModel)
    }

    var body: some View {
        VStack(spacing: 0) {
            // Custom Top Bar
            assistantTopBar

            if let statusText = viewModel.assistantStatusText {
                assistantStatusChip(text: statusText)
                    .padding(.horizontal, 16)
                    .padding(.bottom, 8)
                    .transition(.opacity.combined(with: .move(edge: .top)))
            }
            
            // Chat area
            if viewModel.messages.isEmpty && !viewModel.isSending {
                welcomeScreen
            } else {
                chatMessagesList
            }
            
            // Input Bar
            chatInputBar
        }
        .pageEnterAnimation()
        .animation(.easeInOut(duration: 0.2), value: viewModel.assistantStatusText)
        .toolbar {
            ToolbarItemGroup(placement: .keyboard) {
                Spacer()
                Button("Done") {
                    isInputFocused = false
                }
            }
        }
        .task {
            await viewModel.onAppear()
        }
        .onDisappear {
            pendingAutoScrollWorkItem?.cancel()
            pendingAutoScrollWorkItem = nil
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
                viewModel.clearChat()
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

    private func assistantStatusChip(text: String) -> some View {
        let isRateLimitWarning =
            text.localizedCaseInsensitiveContains("rate limit") ||
            text.localizedCaseInsensitiveContains("quota")

        return HStack(spacing: 8) {
            Image(systemName: isRateLimitWarning ? "exclamationmark.triangle.fill" : "arrow.triangle.2.circlepath")
                .font(.system(size: 11, weight: .semibold))
                .foregroundColor(isRateLimitWarning ? LiquidGlass.amberWarning : LiquidGlass.tealPrimary)

            Text(text)
                .font(.caption2.weight(.medium))
                .foregroundColor(.white.opacity(0.9))

            Spacer(minLength: 0)
        }
        .padding(.horizontal, 12)
        .padding(.vertical, 8)
        .glassEffect(.regular, in: Capsule())
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
            ScrollView(showsIndicators: false) {
                LazyVStack(spacing: 12) {
                    ForEach(viewModel.messages) { message in
                        ChatBubble(message: message)
                            .id(message.id)
                    }
                    
                    if viewModel.isSending {
                        typingIndicator
                            .id("medical-assistant-typing")
                    }

                    Color.clear
                        .frame(height: 1)
                        .id(Self.chatBottomAnchorId)
                        .background(
                            GeometryReader { geometry in
                                Color.clear.preference(
                                    key: ChatBottomAnchorMaxYPreferenceKey.self,
                                    value: geometry.frame(in: .global).maxY
                                )
                            }
                        )
                }
                .padding(16)
            }
            .scrollDismissesKeyboard(.interactively)
            .simultaneousGesture(
                TapGesture().onEnded {
                    isInputFocused = false
                }
            )
            .background(
                GeometryReader { geometry in
                    Color.clear.preference(
                        key: ChatScrollViewMaxYPreferenceKey.self,
                        value: geometry.frame(in: .global).maxY
                    )
                }
            )
            .onPreferenceChange(ChatBottomAnchorMaxYPreferenceKey.self) { value in
                bottomAnchorMaxY = value
                refreshScrollReviewState()
            }
            .onPreferenceChange(ChatScrollViewMaxYPreferenceKey.self) { value in
                scrollViewMaxY = value
                refreshScrollReviewState()
            }
            .onChange(of: viewModel.messages.count) { _, _ in
                if viewModel.messages.isEmpty {
                    didRunInitialConversationScroll = false
                    userIsReviewingHistory = false
                } else {
                    scheduleSmoothAutoScroll(using: proxy)
                }
            }
            .onChange(of: viewModel.isSending) { _, _ in
                scheduleSmoothAutoScroll(using: proxy)
            }
            .task(id: viewModel.messages.count) {
                guard !didRunInitialConversationScroll else { return }
                guard !viewModel.messages.isEmpty else { return }

                didRunInitialConversationScroll = true
                scheduleSmoothAutoScroll(using: proxy, force: true)
            }
        }
    }

    private func scheduleSmoothAutoScroll(using proxy: ScrollViewProxy, force: Bool = false) {
        guard force || !userIsReviewingHistory else { return }

        pendingAutoScrollWorkItem?.cancel()

        let workItem = DispatchWorkItem {
            scrollToLatest(using: proxy, animated: true, force: force)
        }

        pendingAutoScrollWorkItem = workItem
        DispatchQueue.main.asyncAfter(deadline: .now() + Self.autoScrollDelay, execute: workItem)
    }

    private func scrollToLatest(using proxy: ScrollViewProxy, animated: Bool = true, force: Bool = false) {
        guard force || !userIsReviewingHistory else { return }

        let scrollAction = {
            proxy.scrollTo(Self.chatBottomAnchorId, anchor: .bottom)
        }

        if animated {
            if #available(iOS 17.0, *) {
                withAnimation(.smooth(duration: 0.3, extraBounce: 0)) {
                    scrollAction()
                }
            } else {
                withAnimation(.easeInOut(duration: 0.28)) {
                    scrollAction()
                }
            }
        } else {
            scrollAction()
        }
    }

    private func refreshScrollReviewState() {
        guard scrollViewMaxY > 0, bottomAnchorMaxY > 0 else { return }

        let distanceFromBottom = bottomAnchorMaxY - scrollViewMaxY
        userIsReviewingHistory = distanceFromBottom > Self.nearBottomThreshold
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
                TimelineView(.animation(minimumInterval: 0.08)) { timeline in
                    let now = timeline.date.timeIntervalSinceReferenceDate

                    HStack(spacing: 4) {
                        ForEach(0..<3, id: \.self) { i in
                            let phase = now * 2.8 + Double(i) * 0.22
                            let offset = sin(phase * .pi * 2) * 3.2

                            Circle()
                                .fill(.white.opacity(0.6))
                                .frame(width: 6, height: 6)
                                .offset(y: -offset)
                        }
                    }
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

    private var tabAlignedHorizontalInset: CGFloat {
        horizontalSizeClass == .regular ? LiquidGlass.tabAlignedInsetRegular : LiquidGlass.tabAlignedInsetCompact
    }

    private var medAssistInputCornerRadius: CGFloat {
        28
    }

    private var chatInputRow: some View {
        HStack(spacing: 12) {
            TextField("Ask your health assistant...", text: $viewModel.messageText, axis: .vertical)
                .lineLimit(1...4)
                .focused($isInputFocused)
                .foregroundColor(.white)
                .tint(LiquidGlass.tealPrimary)
                .submitLabel(.send)
                .onSubmit {
                    viewModel.sendMessage()
                }

            Button(action: viewModel.sendMessage) {
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
            .disabled(viewModel.messageText.trimmingCharacters(in: .whitespaces).isEmpty || viewModel.isSending)
            .opacity(viewModel.messageText.trimmingCharacters(in: .whitespaces).isEmpty ? 0.4 : 1)
        }
        .padding(.horizontal, 14)
        .padding(.vertical, 12)
    }

    @ViewBuilder
    private var chatInputShell: some View {
        let shape = RoundedRectangle(cornerRadius: medAssistInputCornerRadius, style: .continuous)

        if #available(iOS 26.0, *) {
            chatInputRow
                .glassEffect(.regular.tint(.primary.opacity(isInputFocused ? 0.12 : 0.06)).interactive(), in: shape)
                .overlay {
                    shape
                        .strokeBorder((isInputFocused ? LiquidGlass.tealPrimary : .white).opacity(isInputFocused ? 0.55 : 0.18), lineWidth: 1)
                }
        } else {
            chatInputRow
                .background(.regularMaterial, in: shape)
                .overlay {
                    shape
                        .strokeBorder((isInputFocused ? LiquidGlass.tealPrimary : .white).opacity(isInputFocused ? 0.55 : 0.16), lineWidth: 1)
                }
        }
    }

    private var chatInputBar: some View {
        chatInputShell
            .frame(maxWidth: .infinity)
            .padding(.horizontal, tabAlignedHorizontalInset)
            .padding(.bottom, 8)
    }
}

private struct ChatBottomAnchorMaxYPreferenceKey: PreferenceKey {
    static let defaultValue: CGFloat = 0

    static func reduce(value: inout CGFloat, nextValue: () -> CGFloat) {
        value = nextValue()
    }
}

private struct ChatScrollViewMaxYPreferenceKey: PreferenceKey {
    static let defaultValue: CGFloat = 0

    static func reduce(value: inout CGFloat, nextValue: () -> CGFloat) {
        value = nextValue()
    }
}
