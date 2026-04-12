import Foundation

@MainActor
final class AppContainer: ObservableObject {
    let engine: MobileEngineWrapper
    @Published var shouldPresentUserProfileEdit: Bool = false
    @Published var shouldPresentPatientProfileEdit: Bool = false

    var shouldPresentProfileEdit: Bool {
        get { shouldPresentUserProfileEdit }
        set { shouldPresentUserProfileEdit = newValue }
    }

    init(engine: MobileEngineWrapper = MobileEngineWrapper()) {
        self.engine = engine
    }
}

