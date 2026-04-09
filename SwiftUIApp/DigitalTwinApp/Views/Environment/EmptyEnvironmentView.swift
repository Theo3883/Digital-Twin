import SwiftUI

struct EmptyEnvironmentView: View {
    let onRefresh: () -> Void

    var body: some View {
        VStack(spacing: 16) {
            Image(systemName: "cloud.sun.fill")
                .font(.system(size: 50))
                .foregroundColor(.white.opacity(0.3))
            Text("No Environment Data")
                .font(.title3)
                .fontWeight(.semibold)
                .foregroundColor(.white)
            Text("Allow location access to see air quality and weather data.")
                .font(.subheadline)
                .foregroundColor(.white.opacity(0.65))
                .multilineTextAlignment(.center)
            Button("Refresh", action: onRefresh)
                .liquidGlassButtonStyle()
        }
        .frame(maxWidth: .infinity)
        .padding(.vertical, 60)
    }
}

