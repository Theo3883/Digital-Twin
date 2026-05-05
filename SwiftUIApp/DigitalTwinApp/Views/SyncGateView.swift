import SwiftUI

struct SyncGateView: View {
    @EnvironmentObject var engineWrapper: MobileEngineWrapper
    @Binding var isVisible: Bool
    @State private var stage: LoadingStage = .connecting
    @State private var opacity: Double = 1
    @State private var hasStarted = false

    var body: some View {
        MauiLoadingSurface(
            model: MauiLoadingVisualState(stage: stage),
            showsBackground: true
        )
        .opacity(opacity)
        .task {
            guard !hasStarted else { return }
            hasStarted = true

            stage = .auth
            try? await Task.sleep(for: .milliseconds(180))

            stage = .merge

            // Keep the loading screen visible until push/pull and cache hydration complete.
            await engineWrapper.bootstrapAppDataForLaunch()

            stage = .ready

            try? await Task.sleep(for: .milliseconds(250))
            withAnimation(.easeOut(duration: 0.3)) {
                opacity = 0
            }
            try? await Task.sleep(for: .milliseconds(300))
            isVisible = false
        }
    }
}

