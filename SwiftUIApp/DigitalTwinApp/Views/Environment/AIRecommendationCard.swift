import SwiftUI

struct AIRecommendationCard: View {
    let reading: EnvironmentReadingInfo

    private var recommendation: String {
        switch reading.airQualityLevel {
        case 0: return "Great air quality today! Perfect for outdoor activities and exercise."
        case 1: return "Air quality is acceptable. Sensitive individuals should consider reducing prolonged outdoor exertion."
        case 2: return "Consider wearing a mask outdoors. Limit vigorous outdoor activities."
        case 3: return "Unhealthy air quality. Stay indoors and keep windows closed. Use air purifiers if available."
        case 4: return "Hazardous conditions. Avoid all outdoor exposure. Seek medical attention if you experience symptoms."
        default: return "Unable to generate recommendation without current air quality data."
        }
    }

    var body: some View {
        HStack(alignment: .top, spacing: 12) {
            Image(systemName: "sparkle")
                .font(.title3)
                .foregroundColor(LiquidGlass.tealPrimary)

            VStack(alignment: .leading, spacing: 8) {
                Text("AI Health Recommendation")
                    .font(.subheadline.weight(.medium))
                    .foregroundColor(.white)
                Text(recommendation)
                    .font(.caption)
                    .foregroundColor(.white.opacity(0.65))
                    .fixedSize(horizontal: false, vertical: true)

                Text("Source: OpenWeatherMap")
                    .font(.system(size: 9))
                    .foregroundColor(.white.opacity(0.3))
            }
        }
        .padding()
        .overlay(alignment: .leading) {
            Rectangle()
                .fill(LiquidGlass.tealPrimary)
                .frame(width: 4)
        }
        .glassEffect(.regular, in: RoundedRectangle(cornerRadius: LiquidGlass.radiusCard))
    }
}

