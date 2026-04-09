import SwiftUI

// MARK: - Design Tokens (ported from MAUI liquid-glass.css)

enum LiquidGlass {
    // Brand colors
    static let tealPrimary = Color(red: 0, green: 212/255, blue: 200/255)       // #00D4C8
    static let tealPrimaryDark = Color(red: 0, green: 150/255, blue: 136/255)   // #009688
    static let redCritical = Color(red: 1, green: 59/255, blue: 48/255)          // #FF3B30
    static let amberWarning = Color(red: 1, green: 149/255, blue: 0)             // #FF9500
    static let greenPositive = Color(red: 52/255, green: 199/255, blue: 89/255)  // #34C759
    static let bluePrimary = Color(red: 0, green: 122/255, blue: 1)              // #007AFF
    static let purpleSleep = Color(red: 175/255, green: 82/255, blue: 222/255)   // #AF52DE
    static let indigoDark = Color(red: 88/255, green: 86/255, blue: 214/255)     // #5856D6

    // Background mesh gradient colors (from MAUI)
    static let bgDark = Color(red: 15/255, green: 32/255, blue: 39/255)         // #0f2027
    static let bgMid1 = Color(red: 32/255, green: 58/255, blue: 67/255)         // #203a43
    static let bgMid2 = Color(red: 44/255, green: 83/255, blue: 100/255)        // #2c5364

    // Radii (from MAUI CSS vars)
    static let radiusCard: CGFloat = 32
    static let radiusInner: CGFloat = 20
    static let radiusChip: CGFloat = 14
    static let radiusPill: CGFloat = 9999
    static let radiusButton: CGFloat = 20
    static let radiusInput: CGFloat = 18

    // Text hierarchy (from MAUI CSS)
    static let textMain = Color.white
    static let textSec = Color.white.opacity(0.65)
    static let textTert = Color.white.opacity(0.4)
}

// MARK: - Mesh Gradient Background

struct MeshGradientBackground: View {
    var body: some View {
        ZStack {
            LiquidGlass.bgDark.ignoresSafeArea()
            RadialGradient(
                colors: [LiquidGlass.bgMid1, .clear],
                center: .topLeading,
                startRadius: 0,
                endRadius: UIScreen.main.bounds.width
            ).ignoresSafeArea()
            RadialGradient(
                colors: [LiquidGlass.bgMid2, .clear],
                center: .topTrailing,
                startRadius: 0,
                endRadius: UIScreen.main.bounds.width
            ).ignoresSafeArea()
            RadialGradient(
                colors: [LiquidGlass.bgDark, .clear],
                center: .bottomTrailing,
                startRadius: 0,
                endRadius: UIScreen.main.bounds.width
            ).ignoresSafeArea()
            RadialGradient(
                colors: [LiquidGlass.bgMid1, .clear],
                center: .bottomLeading,
                startRadius: 0,
                endRadius: UIScreen.main.bounds.width
            ).ignoresSafeArea()
        }
    }
}

// MARK: - Glass Card Modifier

struct GlassCard: ViewModifier {
    var tint: Color?

    func body(content: Content) -> some View {
        content
            .padding()
            .glassEffect(tint.map { .regular.tint($0) } ?? .regular, in: RoundedRectangle(cornerRadius: LiquidGlass.radiusCard))
    }
}

// MARK: - Glass Hero Card Modifier (larger, more prominent)

struct GlassHeroCard: ViewModifier {
    var tint: Color

    func body(content: Content) -> some View {
        content
            .padding(20)
            .glassEffect(.regular.tint(tint), in: RoundedRectangle(cornerRadius: LiquidGlass.radiusCard))
    }
}

// MARK: - Glass Section Header

struct GlassSectionHeader: ViewModifier {
    func body(content: Content) -> some View {
        content
            .font(.headline)
            .padding(.horizontal, 16)
            .padding(.vertical, 8)
            .glassEffect(.regular, in: RoundedRectangle(cornerRadius: LiquidGlass.radiusChip))
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
            .glassEffect(.regular.tint(tint), in: RoundedRectangle(cornerRadius: LiquidGlass.radiusChip))
    }
}

// MARK: - Glass Pill Modifier (fully rounded)

struct GlassPill: ViewModifier {
    var tint: Color?

    func body(content: Content) -> some View {
        content
            .padding(.horizontal, 14)
            .padding(.vertical, 8)
            .glassEffect(tint.map { .regular.tint($0) } ?? .regular, in: RoundedRectangle(cornerRadius: LiquidGlass.radiusButton))
    }
}

// MARK: - Glass Banner (full-width alert)

struct GlassBanner: ViewModifier {
    var tint: Color

    func body(content: Content) -> some View {
        content
            .padding()
            .frame(maxWidth: .infinity)
            .glassEffect(.regular.tint(tint), in: RoundedRectangle(cornerRadius: LiquidGlass.radiusInner))
    }
}

// MARK: - Glass Input Bar

struct GlassInputBar: ViewModifier {
    func body(content: Content) -> some View {
        content
            .padding(12)
            .glassEffect(.regular.tint(.primary.opacity(0.05)), in: RoundedRectangle(cornerRadius: LiquidGlass.radiusInput))
    }
}

// MARK: - Page Enter Animation

struct PageEnterAnimation: ViewModifier {
    @State private var appeared = false

    func body(content: Content) -> some View {
        content
            .opacity(appeared ? 1 : 0)
            .offset(y: appeared ? 0 : 8)
            .animation(.easeOut(duration: 0.22), value: appeared)
            .onAppear { appeared = true }
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

    func glassPill(tint: Color? = nil) -> some View {
        modifier(GlassPill(tint: tint))
    }

    func glassBanner(tint: Color) -> some View {
        modifier(GlassBanner(tint: tint))
    }

    func glassInputBar() -> some View {
        modifier(GlassInputBar())
    }

    func pageEnterAnimation() -> some View {
        modifier(PageEnterAnimation())
    }
}
