import SwiftUI

/// Animated body mannequin visualization showing real-time vital data
struct BodyMannequinView: View {
    let heartRate: Double
    let oxygenLevel: Double
    let steps: Int

    @State private var heartPulse = false
    @State private var breathPhase = false

    private var heartColor: Color {
        if heartRate > 120 { return LiquidGlass.redCritical }
        if heartRate > 100 { return LiquidGlass.amberWarning }
        return LiquidGlass.tealPrimary
    }

    private var lungColor: Color {
        if oxygenLevel < 90 { return LiquidGlass.redCritical }
        if oxygenLevel < 95 { return LiquidGlass.amberWarning }
        return LiquidGlass.bluePrimary
    }

    private var activityColor: Color {
        if steps > 8000 { return LiquidGlass.greenPositive }
        if steps > 3000 { return LiquidGlass.amberWarning }
        return .gray
    }

    var body: some View {
        ZStack {
            // Body outline
            BodyOutlineShape()
                .stroke(.white.opacity(0.15), lineWidth: 1.5)
                .frame(width: 120, height: 200)

            // Heart zone
            Circle()
                .fill(heartColor.opacity(0.5))
                .frame(width: heartPulse ? 22 : 16, height: heartPulse ? 22 : 16)
                .shadow(color: heartColor.opacity(0.6), radius: heartPulse ? 10 : 4)
                .offset(x: -4, y: -32)

            // Lung zones
            Ellipse()
                .fill(lungColor.opacity(0.3))
                .frame(width: breathPhase ? 24 : 18, height: breathPhase ? 30 : 22)
                .offset(x: -14, y: -20)
            Ellipse()
                .fill(lungColor.opacity(0.3))
                .frame(width: breathPhase ? 24 : 18, height: breathPhase ? 30 : 22)
                .offset(x: 14, y: -20)

            // Activity indicators (legs)
            Circle()
                .fill(activityColor.opacity(0.4))
                .frame(width: 8, height: 8)
                .offset(x: -12, y: 70)
            Circle()
                .fill(activityColor.opacity(0.4))
                .frame(width: 8, height: 8)
                .offset(x: 12, y: 70)

            // Labels
            VStack(spacing: 2) {
                if heartRate > 0 {
                    Text("\(Int(heartRate))")
                        .font(.system(size: 10, weight: .bold, design: .rounded))
                        .foregroundColor(heartColor)
                }
            }
            .offset(x: 30, y: -32)

            if oxygenLevel > 0 {
                Text("\(Int(oxygenLevel))%")
                    .font(.system(size: 9, weight: .medium, design: .rounded))
                    .foregroundColor(lungColor)
                    .offset(x: 34, y: -12)
            }
        }
        .frame(width: 140, height: 220)
        .onAppear {
            withAnimation(.easeInOut(duration: 0.8).repeatForever(autoreverses: true)) {
                heartPulse = true
            }
            withAnimation(.easeInOut(duration: 2.5).repeatForever(autoreverses: true)) {
                breathPhase = true
            }
        }
    }
}

// MARK: - Body Outline Shape

struct BodyOutlineShape: Shape {
    func path(in rect: CGRect) -> Path {
        var path = Path()
        let w = rect.width
        let h = rect.height
        let cx = w / 2

        // Head
        path.addEllipse(in: CGRect(x: cx - 14, y: 0, width: 28, height: 30))

        // Neck
        path.move(to: CGPoint(x: cx - 6, y: 30))
        path.addLine(to: CGPoint(x: cx - 6, y: 38))
        path.move(to: CGPoint(x: cx + 6, y: 30))
        path.addLine(to: CGPoint(x: cx + 6, y: 38))

        // Torso
        path.move(to: CGPoint(x: cx - 22, y: 38))
        path.addLine(to: CGPoint(x: cx + 22, y: 38))
        path.addLine(to: CGPoint(x: cx + 18, y: h * 0.52))
        path.addLine(to: CGPoint(x: cx - 18, y: h * 0.52))
        path.closeSubpath()

        // Left arm
        path.move(to: CGPoint(x: cx - 22, y: 42))
        path.addQuadCurve(to: CGPoint(x: cx - 42, y: h * 0.42), control: CGPoint(x: cx - 38, y: 48))
        path.addLine(to: CGPoint(x: cx - 38, y: h * 0.42))
        path.addQuadCurve(to: CGPoint(x: cx - 22, y: 50), control: CGPoint(x: cx - 34, y: 52))

        // Right arm
        path.move(to: CGPoint(x: cx + 22, y: 42))
        path.addQuadCurve(to: CGPoint(x: cx + 42, y: h * 0.42), control: CGPoint(x: cx + 38, y: 48))
        path.addLine(to: CGPoint(x: cx + 38, y: h * 0.42))
        path.addQuadCurve(to: CGPoint(x: cx + 22, y: 50), control: CGPoint(x: cx + 34, y: 52))

        // Left leg
        path.move(to: CGPoint(x: cx - 16, y: h * 0.52))
        path.addLine(to: CGPoint(x: cx - 18, y: h * 0.92))
        path.addLine(to: CGPoint(x: cx - 10, y: h * 0.92))
        path.addLine(to: CGPoint(x: cx - 6, y: h * 0.52))

        // Right leg
        path.move(to: CGPoint(x: cx + 16, y: h * 0.52))
        path.addLine(to: CGPoint(x: cx + 18, y: h * 0.92))
        path.addLine(to: CGPoint(x: cx + 10, y: h * 0.92))
        path.addLine(to: CGPoint(x: cx + 6, y: h * 0.52))

        return path
    }
}
