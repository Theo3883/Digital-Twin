import Foundation

/// Creates and caches a single shared `EcgMonitorViewModel` for the app lifetime.
/// Both the ECG tab and the `BackgroundECGTriageService` use the same instance.
@MainActor
enum EcgMonitorViewModelFactory {

    private static var _shared: EcgMonitorViewModel?

    /// Returns the cached instance, creating it on first call.
    static func makeShared(engine: MobileEngineWrapper) -> EcgMonitorViewModel {
        if let existing = _shared { return existing }
        let repo = EngineEcgRepository(engine: engine)
        let vm = EcgMonitorViewModel(
            repository: repo,
            evaluate: EvaluateEcgFrameUseCase(repository: repo)
        )
        _shared = vm
        return vm
    }

    /// Read-only accessor — returns nil if not yet created.
    static var shared: EcgMonitorViewModel? { _shared }
}
