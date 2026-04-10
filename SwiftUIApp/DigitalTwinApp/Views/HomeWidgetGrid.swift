import SwiftUI

struct HomeWidgetGrid: View {
    let heartRate: Double
    let spO2: Double
    let steps: Double
    let envReading: EnvironmentReadingInfo?
    let sleepMinutes: Int
    let sleepQuality: Double
    let insightText: String?
    let hasProfile: Bool
    @Binding var selectedTab: Int

    private var spO2Status: String {
        if spO2 >= 95 { return "Normal" }
        if spO2 >= 90 { return "Low" }
        return "Critical"
    }

    private var spO2StatusColor: Color {
        if spO2 >= 95 { return LiquidGlass.greenPositive }
        if spO2 >= 90 { return LiquidGlass.amberWarning }
        return LiquidGlass.redCritical
    }

    private var sleepQualityLabel: String {
        if sleepQuality >= 80 { return "Optimal" }
        if sleepQuality >= 60 { return "Fair" }
        return "Poor"
    }

    var body: some View {
        let columns = [GridItem(.flexible(), spacing: 12), GridItem(.flexible(), spacing: 12)]

        VStack(spacing: 12) {
            // 1. Heart Rate Hero Card (full width)
            heartRateCard

            // 2×2 grid: Steps, SpO2, Environment, Sleep
            LazyVGrid(columns: columns, spacing: 12) {
                stepsCard
                spO2Card
                environmentCard
                sleepCard
            }
        }

        // AI Insight Hero (full width)
        aiInsightHeroCard
    }

    // MARK: Heart Rate Hero

    private var heartRateCard: some View {
        ZStack(alignment: .bottom) {
            VStack(alignment: .leading, spacing: 12) {
                HStack {
                    ZStack {
                        Circle()
                            .fill(LiquidGlass.redCritical.opacity(0.2))
                            .frame(width: 32, height: 32)
                        Image(systemName: "heart.fill")
                            .font(.system(size: 14))
                            .foregroundColor(LiquidGlass.redCritical)
                    }
                    Text("Heart Rate")
                        .font(.subheadline)
                        .foregroundColor(.white.opacity(0.65))
                    Spacer()
                    if hasProfile {
                        Text("Live")
                            .font(.caption2.weight(.semibold))
                            .foregroundColor(LiquidGlass.greenPositive)
                            .padding(.horizontal, 8)
                            .padding(.vertical, 3)
                            .background {
                                RoundedRectangle(cornerRadius: LiquidGlass.radiusChip)
                                    .fill(LiquidGlass.greenPositive.opacity(0.15))
                            }
                    }
                }

                if hasProfile {
                    HStack(alignment: .firstTextBaseline, spacing: 4) {
                        Text(heartRate > 0 ? String(format: "%.0f", heartRate) : "--")
                            .font(.system(size: 56, weight: .bold, design: .rounded))
                            .foregroundColor(.white)
                        Text("BPM")
                            .font(.caption)
                            .foregroundColor(.white.opacity(0.4))
                    }

                    Text(heartRate > 0 ? "\(String(format: "%.0f", heartRate)) BPM · Live" : "Waiting for data…")
                        .font(.caption.weight(.medium))
                        .foregroundColor(LiquidGlass.redCritical.opacity(0.8))
                } else {
                    Button {
                        selectedTab = 4
                    } label: {
                        HStack(spacing: 8) {
                            Image(systemName: "person.crop.circle.badge.plus")
                                .font(.title2)
                                .foregroundColor(.white.opacity(0.4))
                            Text("Set up your patient profile")
                                .font(.subheadline)
                                .foregroundColor(.white.opacity(0.5))
                            Spacer()
                            Image(systemName: "chevron.right")
                                .font(.caption)
                                .foregroundColor(.white.opacity(0.3))
                        }
                    }
                    .padding(.vertical, 8)
                }
            }
            .frame(maxWidth: .infinity, alignment: .leading)
            .zIndex(1)

            // ECG sparkline decoration
            if hasProfile {
                EcgSparkline()
                    .frame(height: 96)
                    .opacity(0.3)
            }
        }
        .glassCard(tint: LiquidGlass.redCritical.opacity(0.08))
    }

    // MARK: Steps

    private var stepsCard: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack {
                ZStack {
                    Circle()
                        .fill(LiquidGlass.amberWarning.opacity(0.2))
                        .frame(width: 32, height: 32)
                    Image(systemName: "figure.walk")
                        .font(.system(size: 14))
                        .foregroundColor(LiquidGlass.amberWarning)
                }
                Spacer()
            }
            Text("Steps")
                .font(.caption)
                .foregroundColor(.white.opacity(0.65))
            Text(steps > 0 ? String(format: "%.0f", steps) : "--")
                .font(.title2.weight(.bold))
                .foregroundColor(.white)
            // Progress bar
            GeometryReader { geo in
                ZStack(alignment: .leading) {
                    Capsule()
                        .fill(.white.opacity(0.1))
                        .frame(height: 4)
                    Capsule()
                        .fill(LiquidGlass.amberWarning)
                        .frame(width: geo.size.width * min(steps / 10000, 1), height: 4)
                }
            }
            .frame(height: 4)
        }
        .frame(minHeight: 140)
        .glassCard()
    }

    // MARK: SpO2

    private var spO2Card: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack {
                ZStack {
                    Circle()
                        .fill(LiquidGlass.bluePrimary.opacity(0.2))
                        .frame(width: 32, height: 32)
                    Image(systemName: "lungs.fill")
                        .font(.system(size: 14))
                        .foregroundColor(LiquidGlass.bluePrimary)
                }
                Spacer()
            }
            Text("Blood Oxygen")
                .font(.caption)
                .foregroundColor(.white.opacity(0.65))
            Text(spO2 > 0 ? String(format: "%.1f%%", spO2) : "--")
                .font(.title2.weight(.bold))
                .foregroundColor(.white)
            Text(spO2 > 0 ? spO2Status : "No data")
                .font(.caption2)
                .foregroundColor(spO2 > 0 ? spO2StatusColor : .white.opacity(0.4))
        }
        .frame(minHeight: 140)
        .glassCard()
    }

    // MARK: Environment

    private var environmentCard: some View {
        VStack(alignment: .leading, spacing: 8) {
            HStack {
                ZStack {
                    Circle()
                        .fill(LiquidGlass.greenPositive.opacity(0.2))
                        .frame(width: 32, height: 32)
                    Image(systemName: "location.fill")
                        .font(.system(size: 14))
                        .foregroundColor(LiquidGlass.greenPositive)
                }
                Spacer()
            }
            Text(envReading?.locationDisplayName ?? "Unknown")
                .font(.caption)
                .foregroundColor(.white.opacity(0.65))
            if let temp = envReading?.temperature {
                Text(String(format: "%.0f°", temp))
                    .font(.title2.weight(.bold))
                    .foregroundColor(.white)
            } else {
                Text("--")
                    .font(.title2.weight(.bold))
                    .foregroundColor(.white.opacity(0.4))
            }
            if let aqi = envReading?.aqiIndex {
                Text("AQI \(aqi) · \(envReading?.airQualityDisplay ?? "")")
                    .font(.caption2)
                    .foregroundColor(.white.opacity(0.5))
            } else {
                Text("No data")
                    .font(.caption2)
                    .foregroundColor(.white.opacity(0.4))
            }
        }
        .frame(minHeight: 140)
        .glassCard(tint: LiquidGlass.greenPositive.opacity(0.08))
    }

    // MARK: Sleep

    private var sleepCard: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack {
                ZStack {
                    Circle()
                        .fill(Color(red: 99/255, green: 102/255, blue: 241/255).opacity(0.2))
                        .frame(width: 32, height: 32)
                    Image(systemName: "moon.fill")
                        .font(.system(size: 14))
                        .foregroundColor(LiquidGlass.purpleSleep)
                }
                Spacer()
            }
            Text("Sleep")
                .font(.caption)
                .foregroundColor(.white.opacity(0.65))
            if sleepMinutes > 0 {
                Text("\(sleepMinutes / 60)h \(sleepMinutes % 60)m")
                    .font(.title2.weight(.bold))
                    .foregroundColor(.white)
                Text(sleepQualityLabel)
                    .font(.caption2)
                    .foregroundColor(LiquidGlass.purpleSleep)
            } else {
                Text("--")
                    .font(.title2.weight(.bold))
                    .foregroundColor(.white.opacity(0.4))
                Text("No data")
                    .font(.caption2)
                    .foregroundColor(.white.opacity(0.4))
            }
        }
        .frame(minHeight: 140)
        .glassCard()
    }

    // MARK: AI Insight Hero

    private var aiInsightHeroCard: some View {
        HStack(spacing: 12) {
            ZStack {
                Circle()
                    .fill(LiquidGlass.tealPrimary.opacity(0.15))
                    .frame(width: 36, height: 36)
                Image(systemName: "sparkle")
                    .font(.system(size: 16))
                    .foregroundColor(LiquidGlass.tealPrimary)
            }

            VStack(alignment: .leading, spacing: 4) {
                Text("MedAssist Insights")
                    .font(.subheadline.weight(.semibold))
                    .foregroundColor(.white)
                Text(insightText ?? "Tap to chat with your health assistant.")
                    .font(.caption)
                    .foregroundColor(.white.opacity(0.65))
                    .lineLimit(2)
            }

            Spacer()

            Image(systemName: "chevron.right")
                .font(.caption)
                .foregroundColor(.white.opacity(0.3))
        }
        .frame(maxWidth: .infinity)
        .glassCard(tint: LiquidGlass.tealPrimary.opacity(0.06))
        .padding(.top, 0)
    }
}

