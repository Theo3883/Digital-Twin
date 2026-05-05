import SwiftUI

enum LoadingStage: String, CaseIterable, Sendable {
    case connecting
    case auth
    case pull
    case merge
    case ready

    var stepIndex: Int {
        switch self {
        case .connecting, .auth:
            return 0
        case .pull:
            return 1
        case .merge:
            return 2
        case .ready:
            return 3
        }
    }

    var progress: CGFloat {
        switch self {
        case .connecting:
            return 0.05
        case .auth:
            return 0.15
        case .pull:
            return 0.40
        case .merge:
            return 0.75
        case .ready:
            return 1.0
        }
    }

    var statusText: String {
        switch self {
        case .connecting:
            return "Connecting to cloud..."
        case .auth:
            return "Authenticating..."
        case .pull:
            return "Pulling health records..."
        case .merge:
            return "Merging local data..."
        case .ready:
            return "All done!"
        }
    }
}

struct MauiLoadingVisualState: Equatable, Sendable {
    let stage: LoadingStage
    let title: String
    let subtitle: String
    let tagline: String

    init(
        stage: LoadingStage,
        title: String = "Digital Twin",
        subtitle: String = "Your personal health companion",
        tagline: String = "Securing your data end-to-end"
    ) {
        self.stage = stage
        self.title = title
        self.subtitle = subtitle
        self.tagline = tagline
    }

    static let steps = [
        "Authenticating",
        "Pulling health records",
        "Merging local data",
        "Ready"
    ]
}

struct LoadingView: View {
    let model: MauiLoadingVisualState

    init(stage: LoadingStage) {
        self.model = MauiLoadingVisualState(stage: stage)
    }

    init(model: MauiLoadingVisualState) {
        self.model = model
    }

    var body: some View {
        MauiLoadingSurface(model: model, showsBackground: false)
    }
}

struct MauiLoadingSurface: View {
    let model: MauiLoadingVisualState
    let showsBackground: Bool

    var body: some View {
        ZStack {
            if showsBackground {
                MeshGradientBackground()
            }

            FloatingOrb(size: 420, baseOffset: CGSize(width: -100, height: -230), color: LiquidGlass.tealPrimary, delay: 0)
            FloatingOrb(size: 350, baseOffset: CGSize(width: 140, height: 260), color: LiquidGlass.bgMid2, delay: -4)
            FloatingOrb(size: 280, baseOffset: CGSize(width: 80, height: 30), color: LiquidGlass.bgMid1, delay: -8)

            VStack(spacing: 16) {
                VStack(spacing: 0) {
                    HeartbeatIconRing()
                        .padding(.bottom, 20)

                    Text(model.title)
                        .font(.system(size: 28, weight: .bold, design: .rounded))
                        .foregroundStyle(LiquidGlass.textMain)

                    Text(model.subtitle)
                        .font(.system(size: 14, weight: .regular, design: .default))
                        .foregroundStyle(LiquidGlass.textSec)

                    Rectangle()
                        .fill(.white.opacity(0.08))
                        .frame(height: 1)
                        .padding(.top, 20)
                        .padding(.bottom, 16)

                    HStack(spacing: 10) {
                        LoadingArcSpinner()
                        Text(model.stage.statusText)
                            .font(.system(size: 13, weight: .regular, design: .default))
                            .foregroundStyle(LiquidGlass.textSec)
                        Spacer(minLength: 0)
                    }
                    .padding(.bottom, 14)

                    GeometryReader { proxy in
                        ZStack(alignment: .leading) {
                            Capsule()
                                .fill(.white.opacity(0.08))

                            Capsule()
                                .fill(
                                    LinearGradient(
                                        colors: [LiquidGlass.tealPrimaryDark, LiquidGlass.tealPrimary],
                                        startPoint: .leading,
                                        endPoint: .trailing
                                    )
                                )
                                .frame(width: max(4, proxy.size.width * model.stage.progress))
                                .animation(.timingCurve(0.25, 0.46, 0.45, 0.94, duration: 0.6), value: model.stage.progress)
                        }
                    }
                    .frame(height: 4)
                    .padding(.bottom, 20)

                    VStack(spacing: 10) {
                        ForEach(Array(MauiLoadingVisualState.steps.enumerated()), id: \.offset) { index, label in
                            HStack(spacing: 10) {
                                StepIndicator(currentIndex: model.stage.stepIndex, rowIndex: index)
                                Text(label)
                                    .font(.system(size: 13, weight: .regular, design: .default))
                                    .foregroundStyle(stepColor(index: index))
                                Spacer(minLength: 0)
                            }
                        }
                    }
                }
                .padding(.vertical, 28)
                .padding(.horizontal, 28)
                .frame(maxWidth: 360)
                .glassEffect(
                    .regular.tint(.white.opacity(0.06)),
                    in: RoundedRectangle(cornerRadius: LiquidGlass.radiusCard, style: .continuous)
                )
                .pageEnterAnimation()

                Text(model.tagline)
                    .font(.system(size: 11, weight: .regular, design: .default))
                    .foregroundStyle(LiquidGlass.textTert)
                    .tracking(0.3)
            }
            .padding(24)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }

    private func stepColor(index: Int) -> Color {
        if index < model.stage.stepIndex {
            return LiquidGlass.tealPrimary
        }

        if index == model.stage.stepIndex {
            return LiquidGlass.textMain
        }

        return LiquidGlass.textTert
    }
}

private struct FloatingOrb: View {
    let size: CGFloat
    let baseOffset: CGSize
    let color: Color
    let delay: Double

    @State private var isDrifting = false

    var body: some View {
        Circle()
            .fill(
                RadialGradient(
                    colors: [color.opacity(0.6), .clear],
                    center: .center,
                    startRadius: 0,
                    endRadius: size * 0.5
                )
            )
            .frame(width: size, height: size)
            .blur(radius: 80)
            .opacity(0.35)
            .offset(
                x: baseOffset.width + (isDrifting ? 30 : 0),
                y: baseOffset.height + (isDrifting ? -20 : 0)
            )
            .animation(.easeInOut(duration: 12).delay(delay).repeatForever(autoreverses: true), value: isDrifting)
            .onAppear { isDrifting = true }
    }
}

private struct HeartbeatIconRing: View {
    @State private var heartbeatPulse = false
    @State private var glowPulse = false

    var body: some View {
        ZStack {
            Circle()
                .fill(LiquidGlass.tealPrimary.opacity(0.12))
                .frame(width: 80, height: 80)

            Circle()
                .stroke(LiquidGlass.tealPrimary.opacity(0.25), lineWidth: 1)
                .frame(width: 80, height: 80)

            Circle()
                .stroke(LiquidGlass.tealPrimary.opacity(0.4), lineWidth: 1.5)
                .frame(width: 80, height: 80)
                .scaleEffect(glowPulse ? 1.3 : 1)
                .opacity(glowPulse ? 0 : 1)
                .animation(.easeInOut(duration: 2).repeatForever(autoreverses: false), value: glowPulse)

            Image("human")
                .resizable()
                .scaledToFit()
                .frame(width: 44, height: 44)
                .clipShape(Circle())
                .shadow(color: LiquidGlass.tealPrimary.opacity(0.5), radius: 8)
                .scaleEffect(heartbeatPulse ? 1.1 : 1)
                .animation(.timingCurve(0.34, 1.56, 0.64, 1, duration: 0.83).repeatForever(autoreverses: true), value: heartbeatPulse)
        }
        .onAppear {
            heartbeatPulse = true
            glowPulse = true
        }
    }
}

private struct LoadingArcSpinner: View {
    @State private var isSpinning = false

    var body: some View {
        Circle()
            .trim(from: 0.18, to: 0.92)
            .stroke(style: StrokeStyle(lineWidth: 2, lineCap: .round))
            .foregroundStyle(LiquidGlass.tealPrimary)
            .frame(width: 18, height: 18)
            .rotationEffect(.degrees(isSpinning ? 360 : 0))
            .animation(.linear(duration: 0.9).repeatForever(autoreverses: false), value: isSpinning)
            .onAppear { isSpinning = true }
    }
}

private struct StepIndicator: View {
    let currentIndex: Int
    let rowIndex: Int

    var body: some View {
        Group {
            if rowIndex < currentIndex {
                Image(systemName: "checkmark")
                    .font(.system(size: 9, weight: .bold, design: .default))
                    .foregroundStyle(LiquidGlass.tealPrimary)
                    .frame(width: 16, height: 16)
                    .background(Circle().fill(LiquidGlass.tealPrimary.opacity(0.18)))
            } else if rowIndex == currentIndex {
                PulsingStepDot()
                    .frame(width: 16, height: 16)
            } else {
                Circle()
                    .fill(.white.opacity(0.1))
                    .frame(width: 8, height: 8)
                    .frame(width: 16, height: 16)
            }
        }
    }
}

private struct PulsingStepDot: View {
    @State private var pulse = false

    var body: some View {
        Circle()
            .fill(LiquidGlass.tealPrimary)
            .frame(width: 8, height: 8)
            .overlay {
                Circle()
                    .stroke(LiquidGlass.tealPrimary.opacity(0.45), lineWidth: 1)
                    .scaleEffect(pulse ? 2.3 : 1)
                    .opacity(pulse ? 0 : 1)
            }
            .animation(.easeInOut(duration: 1.5).repeatForever(autoreverses: false), value: pulse)
            .onAppear { pulse = true }
    }
}

