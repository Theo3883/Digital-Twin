import Foundation

@MainActor
final class AppContainer: ObservableObject {
    let engine: MobileEngineWrapper

    init(engine: MobileEngineWrapper = MobileEngineWrapper()) {
        self.engine = engine
    }
}

