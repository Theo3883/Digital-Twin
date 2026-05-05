import Foundation
import Combine

/// Runs the AI triage engine continuously in the background
/// whenever the ESP32 is connected — regardless of which view is visible.
///
/// Attach at app startup:
///   BackgroundECGTriageService.shared.attach(ble: ble, viewModel: ecgViewModel)
@MainActor
final class BackgroundECGTriageService: ObservableObject {

    // MARK: - Singleton
    static let shared = BackgroundECGTriageService()

    // MARK: - Published state (mirrored for any interested view)
    @Published private(set) var isRunning: Bool = false

    // MARK: - Private
    private var triageTask: Task<Void, Never>?
    private var cancellables = Set<AnyCancellable>()

    private init() {}

    // MARK: - Attach

    /// Call once from the App entry point after both `BLEManager` and
    /// `EcgMonitorViewModel` are initialised.
    func attach(ble: BLEManager, viewModel: EcgMonitorViewModel) {
        TriageNotificationService.shared.requestPermission()

        // Observe BLE connection changes.
        ble.$isConnected
            .receive(on: RunLoop.main)
            .sink { [weak self] connected in
                guard let self else { return }
                if connected {
                    self.startTriageLoop(ble: ble, viewModel: viewModel)
                } else {
                    self.stopTriageLoop()
                    viewModel.disconnectTriage()
                }
            }
            .store(in: &cancellables)

        // If already connected when this is called, start immediately.
        if ble.isConnected {
            startTriageLoop(ble: ble, viewModel: viewModel)
        }
    }

    // MARK: - Loop control

    private func startTriageLoop(ble: BLEManager, viewModel: EcgMonitorViewModel) {
        guard triageTask == nil else { return } // already running
        viewModel.reconnectTriage()
        isRunning = true
        print("[BackgroundECGTriage] Triage loop started")

        triageTask = Task {
            while !Task.isCancelled {
                await viewModel.evaluateFrame(ble: ble)
                try? await Task.sleep(nanoseconds: 1_000_000_000) // 1 second
            }
        }
    }

    private func stopTriageLoop() {
        triageTask?.cancel()
        triageTask = nil
        isRunning = false
        print("[BackgroundECGTriage] Triage loop stopped")
    }

}
