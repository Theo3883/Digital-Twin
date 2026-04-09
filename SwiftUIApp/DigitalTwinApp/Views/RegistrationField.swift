import SwiftUI

struct RegistrationField<Content: View>: View {
    let icon: String
    let placeholder: String
    @ViewBuilder let content: Content

    var body: some View {
        HStack(spacing: 12) {
            Image(systemName: icon)
                .foregroundColor(LiquidGlass.tealPrimary)
                .frame(width: 20)
            content
        }
        .padding(14)
        .glassEffect(.regular.tint(.primary.opacity(0.05)), in: RoundedRectangle(cornerRadius: LiquidGlass.radiusInput))
    }
}

