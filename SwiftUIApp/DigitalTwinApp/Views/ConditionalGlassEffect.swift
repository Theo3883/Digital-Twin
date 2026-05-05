import SwiftUI

struct ConditionalGlassEffect<S: InsettableShape>: ViewModifier {
    let isEnabled: Bool
    let shape: S

    func body(content: Content) -> some View {
        if isEnabled {
            content.glassEffect(.regular, in: shape)
        } else {
            content
        }
    }
}

