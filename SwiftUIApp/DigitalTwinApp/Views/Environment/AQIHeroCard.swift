import SwiftUI

struct AQIHeroCard: View {
    let reading: EnvironmentReadingInfo
    var onEditLocation: () -> Void
    var onRefresh: () -> Void

    private let heroImageURL = URL(string: "https://picsum.photos/seed/bucharest/400/200")

    private var aqiColor: Color {
        switch reading.airQualityLevel {
        case 0:
            return LiquidGlass.greenPositive
        case 1:
            return LiquidGlass.amberWarning
        default:
            return LiquidGlass.redCritical
        }
    }

    private var healthGuidance: String {
        switch reading.airQualityLevel {
        case 0:
            return "Air quality is good. Outdoor activity is generally fine for most people."
        case 1:
            return "Air quality is acceptable. Sensitive groups may want to shorten outdoor exertion."
        default:
            return "Air quality may affect health. Limit outdoor activity and check local guidance."
        }
    }

    private var locationAndDateText: String {
        let location = reading.locationDisplayName ?? "Current Location"
        let date = reading.timestamp.formatted(.dateTime.day().month(.abbreviated).year())
        return "\(location) · \(date)"
    }

    private var heroGradient: LinearGradient {
        LinearGradient(
            colors: [
                Color(red: 30 / 255, green: 60 / 255, blue: 120 / 255).opacity(0.4),
                Color(red: 10 / 255, green: 14 / 255, blue: 26 / 255).opacity(0.9)
            ],
            startPoint: .top,
            endPoint: .bottom
        )
    }

    @ViewBuilder
    private var heroBackground: some View {
        ZStack {
            if let heroImageURL {
                AsyncImage(url: heroImageURL) { phase in
                    switch phase {
                    case .success(let image):
                        image
                            .resizable()
                            .scaledToFill()
                    case .empty, .failure:
                        heroGradient
                    @unknown default:
                        heroGradient
                    }
                }
                .frame(maxWidth: .infinity, maxHeight: .infinity)
                .blur(radius: 2)
                .overlay {
                    heroGradient.opacity(0.6)
                }
            } else {
                heroGradient
            }

            LinearGradient(
                colors: [Color.clear, Color.black.opacity(0.35)],
                startPoint: .top,
                endPoint: .bottom
            )
        }
    }

    var body: some View {
        ZStack(alignment: .topTrailing) {
            VStack(alignment: .leading, spacing: 0) {
                Spacer(minLength: 0)

                HStack(spacing: 6) {
                    Image(systemName: "mappin")
                        .font(.system(size: 14))
                        .foregroundColor(.white.opacity(0.8))
                    Text(locationAndDateText)
                        .font(.system(size: 14))
                        .foregroundColor(.white.opacity(0.8))
                    Spacer()
                }
                .padding(.bottom, 12)

                HStack(spacing: 0) {
                    Text("AQI \(reading.aqiIndex)")
                        .font(.system(size: 22, weight: .bold, design: .default))
                    Text(" · \(reading.airQualityDisplay.uppercased())")
                        .font(.system(size: 22, weight: .bold, design: .default))
                }
                .foregroundColor(aqiColor)
                .padding(.horizontal, 16)
                .padding(.vertical, 8)
                .background {
                    RoundedRectangle(cornerRadius: 16, style: .continuous)
                        .fill(aqiColor.opacity(0.2))
                        .overlay {
                            RoundedRectangle(cornerRadius: 16, style: .continuous)
                                .strokeBorder(aqiColor.opacity(0.4), lineWidth: 1)
                        }
                }
                .padding(.bottom, 8)

                Text(healthGuidance)
                    .font(.system(size: 12))
                    .foregroundColor(.white.opacity(0.7))
                    .multilineTextAlignment(.leading)
                    .lineLimit(2)
            }
            .frame(maxWidth: .infinity, alignment: .leading)
            .frame(height: 200)
            .padding(20)
            .background {
                heroBackground
            }
            .clipShape(RoundedRectangle(cornerRadius: LiquidGlass.radiusCard, style: .continuous))
            .glassEffect(.regular, in: RoundedRectangle(cornerRadius: LiquidGlass.radiusCard, style: .continuous))

            Menu {
                Button { onEditLocation() } label: {
                    Label("Edit Location", systemImage: "location")
                }
                Button { onRefresh() } label: {
                    Label("Refresh Now", systemImage: "arrow.clockwise")
                }
            } label: {
                VStack(spacing: 3) {
                    Circle().fill(Color.white).frame(width: 3.5, height: 3.5)
                    Circle().fill(Color.white).frame(width: 3.5, height: 3.5)
                    Circle().fill(Color.white).frame(width: 3.5, height: 3.5)
                }
                    .frame(width: 32, height: 32)
                    .background(
                        Circle().fill(Color.black.opacity(0.35))
                    )
                    .overlay(
                        Circle()
                            .strokeBorder(Color.white.opacity(0.08), lineWidth: 1)
                    )
            }
            .padding(12)
        }
    }
}

