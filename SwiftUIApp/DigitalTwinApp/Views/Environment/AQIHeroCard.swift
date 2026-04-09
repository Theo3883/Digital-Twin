import SwiftUI

struct AQIHeroCard: View {
    let reading: EnvironmentReadingInfo
    var onEditLocation: () -> Void
    var onRefresh: () -> Void

    private var aqiColor: Color {
        switch reading.airQualityLevel {
        case 0: return LiquidGlass.greenPositive
        case 1, 2: return LiquidGlass.amberWarning
        case 3, 4: return LiquidGlass.redCritical
        default: return .gray
        }
    }

    private var healthGuidance: String {
        switch reading.airQualityLevel {
        case 0: return "Air quality is ideal for most activities"
        case 1: return "Sensitive groups should limit prolonged outdoor exertion"
        case 2: return "Everyone may begin to experience health effects"
        case 3: return "Health alert — everyone may experience serious effects"
        case 4: return "Emergency conditions — avoid all outdoor activity"
        default: return "No data available"
        }
    }

    var body: some View {
        ZStack(alignment: .topTrailing) {
            VStack(spacing: 8) {
                HStack(spacing: 6) {
                    Image(systemName: "mappin")
                        .font(.system(size: 14))
                        .foregroundColor(.white.opacity(0.8))
                    let loc = reading.locationDisplayName ?? "Current Location"
                    let date = reading.timestamp.formatted(.dateTime.day().month(.abbreviated).year())
                    Text("\(loc) · \(date)")
                        .font(.system(size: 14))
                        .foregroundColor(.white.opacity(0.8))
                    Spacer()
                }

                Spacer()

                HStack(spacing: 6) {
                    if let aqi = reading.aqiIndex {
                        Text("AQI \(aqi)")
                            .font(.system(size: 22, weight: .bold, design: .default))
                    }
                    Text("· \(reading.airQualityDisplay)")
                        .font(.system(size: 22, weight: .bold, design: .default))
                }
                .foregroundColor(aqiColor)
                .padding(.horizontal, 14)
                .padding(.vertical, 6)
                .background {
                    RoundedRectangle(cornerRadius: 16)
                        .fill(aqiColor.opacity(0.15))
                        .overlay {
                            RoundedRectangle(cornerRadius: 16)
                                .strokeBorder(aqiColor.opacity(0.35), lineWidth: 1)
                        }
                }

                Text(healthGuidance)
                    .font(.caption)
                    .foregroundColor(.white.opacity(0.7))
                    .multilineTextAlignment(.center)

                Spacer()
            }
            .frame(maxWidth: .infinity)
            .frame(height: 200)
            .padding()
            .background {
                ZStack {
                    LinearGradient(
                        colors: [aqiColor.opacity(0.4), aqiColor.opacity(0.1), Color.clear],
                        startPoint: .top,
                        endPoint: .bottom
                    )
                    LinearGradient(
                        colors: [Color.clear, Color.black.opacity(0.3)],
                        startPoint: .top,
                        endPoint: .bottom
                    )
                }
            }
            .clipShape(RoundedRectangle(cornerRadius: LiquidGlass.radiusCard))
            .glassEffect(.regular, in: RoundedRectangle(cornerRadius: LiquidGlass.radiusCard))

            Menu {
                Button { onEditLocation() } label: {
                    Label("Edit Location", systemImage: "location")
                }
                Button { onRefresh() } label: {
                    Label("Refresh Now", systemImage: "arrow.clockwise")
                }
            } label: {
                Image(systemName: "ellipsis")
                    .font(.system(size: 16, weight: .medium))
                    .foregroundColor(.white.opacity(0.6))
                    .frame(width: 36, height: 36)
            }
            .padding(12)
        }
    }
}

