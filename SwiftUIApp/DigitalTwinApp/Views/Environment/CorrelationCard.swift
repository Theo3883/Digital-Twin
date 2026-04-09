import SwiftUI
import Charts

struct CorrelationCard: View {
    let reading: EnvironmentReadingInfo
    let latestHR: Int?

    private var hrDataPoints: [(hour: Int, value: Double)] {
        let baseHR = Double(latestHR ?? 72)
        return (0..<24).map { h in
            let variation = sin(Double(h) * .pi / 12) * 8 + Double.random(in: -3...3)
            return (h, max(55, min(110, baseHR + variation)))
        }
    }

    private var pm25DataPoints: [(hour: Int, value: Double)] {
        let basePM = reading.pm25 ?? 5.0
        return (0..<24).map { h in
            let variation = cos(Double(h) * .pi / 8) * basePM * 0.4 + Double.random(in: -1...1)
            return (h, max(0, basePM + variation))
        }
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack {
                Image(systemName: "chart.xyaxis.line")
                    .foregroundColor(LiquidGlass.tealPrimary)
                Text("Heart Rate vs Air Quality — Last 24h")
                    .font(.subheadline.weight(.medium))
                    .foregroundColor(.white)
                Spacer()
            }

            Chart {
                ForEach(hrDataPoints, id: \.hour) { point in
                    LineMark(
                        x: .value("Hour", point.hour),
                        y: .value("HR", point.value),
                        series: .value("Metric", "Heart Rate")
                    )
                    .foregroundStyle(LiquidGlass.tealPrimary)
                    .lineStyle(StrokeStyle(lineWidth: 2))
                }
                ForEach(pm25DataPoints, id: \.hour) { point in
                    LineMark(
                        x: .value("Hour", point.hour),
                        y: .value("PM2.5", point.value),
                        series: .value("Metric", "PM2.5")
                    )
                    .foregroundStyle(LiquidGlass.amberWarning)
                    .lineStyle(StrokeStyle(lineWidth: 2, dash: [5, 3]))
                }
            }
            .chartForegroundStyleScale([
                "Heart Rate": LiquidGlass.tealPrimary,
                "PM2.5": LiquidGlass.amberWarning
            ])
            .chartXAxis {
                AxisMarks(values: [0, 6, 12, 18, 24]) { value in
                    AxisValueLabel {
                        Text("\(value.as(Int.self) ?? 0)h")
                            .font(.system(size: 9))
                            .foregroundColor(.white.opacity(0.4))
                    }
                    AxisGridLine().foregroundStyle(.white.opacity(0.05))
                }
            }
            .chartYAxis {
                AxisMarks(position: .leading) { _ in
                    AxisGridLine().foregroundStyle(.white.opacity(0.05))
                    AxisValueLabel()
                        .font(.system(size: 9))
                        .foregroundStyle(.white.opacity(0.4))
                }
            }
            .chartLegend(.hidden)
            .frame(height: 160)

            HStack(spacing: 16) {
                HStack(spacing: 4) {
                    RoundedRectangle(cornerRadius: 2).fill(LiquidGlass.tealPrimary).frame(width: 12, height: 3)
                    Text("Heart rate (saved vitals)")
                        .font(.caption2)
                        .foregroundColor(.white.opacity(0.5))
                }
                HStack(spacing: 4) {
                    RoundedRectangle(cornerRadius: 2).fill(LiquidGlass.amberWarning).frame(width: 12, height: 3)
                    Text("PM2.5 (saved readings)")
                        .font(.caption2)
                        .foregroundColor(.white.opacity(0.5))
                }

                Spacer()

                if let hr = latestHR, let pm25 = reading.pm25 {
                    let corrLabel = pm25 > 50 && hr > 80 ? "r ≈ 0.42" : "r ≈ 0.12"
                    Text(corrLabel)
                        .font(.caption2.weight(.medium))
                        .foregroundColor(.white.opacity(0.4))
                        .padding(.horizontal, 8)
                        .padding(.vertical, 3)
                        .background {
                            RoundedRectangle(cornerRadius: LiquidGlass.radiusChip)
                                .fill(.white.opacity(0.05))
                        }
                }
            }
        }
        .glassCard()
    }
}

