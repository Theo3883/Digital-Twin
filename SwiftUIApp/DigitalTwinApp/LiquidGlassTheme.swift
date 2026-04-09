import SwiftUI

// MARK: - Design Tokens (ported from MAUI liquid-glass.css)

enum LiquidGlass {
    // Brand colors
    static let tealPrimary = Color(red: 0, green: 212/255, blue: 200/255)     // #00D4C8
    static let redCritical = Color(red: 1, green: 59/255, blue: 48/255)        // #FF3B30
    static let amberWarning = Color(red: 1, green: 149/255, blue: 0)           // #FF9500
    static let greenPositive = Color(red: 52/255, green: 199/255, blue: 89/255) // #34C759
    static let bluePrimary = Color(red: 0, green: 122/255, blue: 1)            // #007AFF
    static let purpleSleep = Color(red: 175/255, green: 82/255, blue: 222/255) // #AF52DE
    static let indigoDark = Color(red: 88/255, green: 86/255, blue: 214/255)   // #5856D6
}

// MARK: - Glass Card Modifier

struct GlassCard: ViewModifier {
    var tint: Color?

    func body(content: Content) -> some View {
        content
            .padding()
            .background {
                RoundedRectangle(cornerRadius: 16)
                    .glassEffect(tint.map { .regular.tint($0) } ?? .regular)
            }
    }
}

// MARK: - Glass Hero Card Modifier (larger, more prominent)

struct GlassHeroCard: ViewModifier {
    var tint: Color

    func body(content: Content) -> some View {
        content
            .padding(20)
            .background {
                RoundedRectangle(cornerRadius: 20)
                    .glassEffect(.regular.tint(tint))
            }
    }
}

// MARK: - Glass Section Header

struct GlassSectionHeader: ViewModifier {
    func body(content: Content) -> some View {
        content
            .font(.headline)
            .padding(.horizontal, 16)
            .padding(.vertical, 8)
            .background {
                Capsule()
                    .glassEffect()
            }
    }
}

// MARK: - Glass Chip (small tag-like element)

struct GlassChip: ViewModifier {
    var tint: Color

    func body(content: Content) -> some View {
        content
            .font(.caption)
            .fontWeight(.medium)
            .padding(.horizontal, 12)
            .padding(.vertical, 6)
            .background {
                Capsule()
                    .glassEffect(.regular.tint(tint))
            }
    }
}

// MARK: - Glass Banner (full-width alert)

struct GlassBanner: ViewModifier {
    var tint: Color

    func body(content: Content) -> some View {
        content
            .padding()
            .frame(maxWidth: .infinity)
            .background {
                RoundedRectangle(cornerRadius: 12)
                    .glassEffect(.regular.tint(tint))
            }
    }
}

// MARK: - Glass Input Bar

struct GlassInputBar: ViewModifier {
    func body(content: Content) -> some View {
        content
            .padding(12)
            .background {
                RoundedRectangle(cornerRadius: 24)
                    .glassEffect(.regular.tint(.primary.opacity(0.05)))
            }
    }
}

// MARK: - View Extensions

extension View {
    func glassCard(tint: Color? = nil) -> some View {
        modifier(GlassCard(tint: tint))
    }

    func glassHeroCard(tint: Color = LiquidGlass.tealPrimary) -> some View {
        modifier(GlassHeroCard(tint: tint))
    }

    func glassSectionHeader() -> some View {
        modifier(GlassSectionHeader())
    }

    func glassChip(tint: Color) -> some View {
        modifier(GlassChip(tint: tint))
    }

    func glassBanner(tint: Color) -> some View {
        modifier(GlassBanner(tint: tint))
    }

    func glassInputBar() -> some View {
        modifier(GlassInputBar())
    }
}
