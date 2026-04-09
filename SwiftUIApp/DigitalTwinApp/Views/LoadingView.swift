import SwiftUI

struct LoadingView: View {
    let message: String

    var body: some View {
        VStack(spacing: 20) {
            ProgressView()
                .scaleEffect(1.5)

            Text(message)
                .font(.headline)
                .foregroundColor(.white.opacity(0.65))
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }
}

