import Foundation

@MainActor
final class AppContainer: ObservableObject {
    let engine: MobileEngineWrapper
    @Published var shouldPresentProfileEdit: Bool = false

    init(engine: MobileEngineWrapper = MobileEngineWrapper()) {
        self.engine = engine
    }
}

