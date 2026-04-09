import SwiftUI

struct LocationInfoBar: View {
    let reading: EnvironmentReadingInfo

    var body: some View {
        HStack(spacing: 8) {
            Image(systemName: "location.fill")
                .font(.caption)
                .foregroundColor(LiquidGlass.tealPrimary)
            Text(reading.locationDisplayName ?? String(format: "%.4f, %.4f", reading.latitude, reading.longitude))
                .font(.caption)
                .foregroundColor(.white.opacity(0.5))
            Spacer()
            Text("Last updated \(reading.timestamp.formatted(date: .omitted, time: .shortened))")
                .font(.caption2)
                .foregroundColor(.white.opacity(0.3))
        }
        .padding(.horizontal, 16)
        .padding(.vertical, 10)
        .glassEffect(.regular, in: RoundedRectangle(cornerRadius: LiquidGlass.radiusButton))
    }
}

