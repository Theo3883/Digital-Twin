import SwiftUI

struct SyncGateView: View {
    @EnvironmentObject var engineWrapper: MobileEngineWrapper
    @Binding var isVisible: Bool
    @State private var syncStep = 0 // 0=connecting, 1=auth, 2=ready
    @State private var opacity: Double = 1

    private let steps = ["Connecting to cloud…", "Authenticating…", "Loading your data…"]

    var body: some View {
        ZStack {
            LiquidGlass.bgDark.ignoresSafeArea()

            VStack(spacing: 32) {
                Image(systemName: "heart.text.square.fill")
                    .font(.system(size: 60))
                    .foregroundStyle(LiquidGlass.tealPrimary)

                VStack(spacing: 16) {
                    ForEach(0..<steps.count, id: \.self) { index in
                        HStack(spacing: 12) {
                            if index < syncStep {
                                Image(systemName: "checkmark.circle.fill")
                                    .foregroundColor(LiquidGlass.greenPositive)
                            } else if index == syncStep {
                                ProgressView()
                            } else {
                                Image(systemName: "circle")
                                    .foregroundColor(.white.opacity(0.3))
                            }

                            Text(steps[index])
                                .font(.subheadline)
                                .foregroundColor(index <= syncStep ? .white : .white.opacity(0.4))

                            Spacer()
                        }
                        .frame(maxWidth: 280)
                    }
                }
            }
        }
        .opacity(opacity)
        .task {
            // Step 0 active — push any pending local changes to cloud
            let _ = await engineWrapper.pushLocalChanges()
            syncStep = 1

            // Step 1 active — full bidirectional sync (pull from cloud)
            let _ = await engineWrapper.performSync()
            syncStep = 2

            // Step 2 active — rebuild in-memory caches from SQLite
            if engineWrapper.medications.isEmpty {
                await engineWrapper.loadMedications()
            }
            await engineWrapper.loadLatestEnvironmentReading()

            // Brief pause so the user can see all three checkmarks before dismissal
            try? await Task.sleep(for: .milliseconds(500))
            withAnimation(.easeOut(duration: 0.3)) {
                opacity = 0
            }
            try? await Task.sleep(for: .milliseconds(300))
            isVisible = false
        }
    }
}

