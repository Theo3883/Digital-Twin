import SwiftUI

extension View {
    /// Applies Liquid Glass prominent button style
    func liquidGlassButtonStyle() -> some View {
        self
            .buttonStyle(.glassProminent)
            .glassEffect(.regular.tint(.blue).interactive())
    }

    /// Applies Liquid Glass card style
    func liquidGlassCardStyle() -> some View {
        self
            .glassEffect(.regular, in: RoundedRectangle(cornerRadius: LiquidGlass.radiusCard))
    }

    /// Applies tinted Liquid Glass card style
    func liquidGlassTintedCard(_ color: Color) -> some View {
        self
            .glassEffect(.regular.tint(color), in: RoundedRectangle(cornerRadius: LiquidGlass.radiusCard))
    }

    /// Applies Liquid Glass navigation bar style
    func liquidGlassNavigationStyle() -> some View {
        self
            .toolbarBackground(.hidden, for: .navigationBar)
    }

    /// Applies Liquid Glass tab view style
    func liquidGlassTabViewStyle() -> some View {
        self
            .tabViewStyle(.tabBarOnly)
            .toolbarBackground(.hidden, for: .tabBar)
    }
}

