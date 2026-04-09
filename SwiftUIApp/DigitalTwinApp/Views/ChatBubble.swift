import SwiftUI

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
            .modifier(
                ConditionalGlassEffect(
                    isEnabled: !message.isUser,
                    shape: UnevenRoundedRectangle(
                        topLeadingRadius: 4, bottomLeadingRadius: 16,
                        bottomTrailingRadius: 16, topTrailingRadius: 16
                    )
                )
            )

            if !message.isUser { Spacer(minLength: 60) }
        }
    }
}

