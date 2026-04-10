import SwiftUI
import Charts

/// 24-hour HR vs PM2.5 correlation chart
struct EnvironmentAnalyticsView: View {
    let analytics: EnvironmentAnalyticsInfo

    private var correlationText: String {
        guard let r = analytics.correlationR else { return "Insufficient data" }
        let absR = abs(r)
        if absR > 0.7 { return "Strong" }
        if absR > 0.4 { return "Moderate" }
        if absR > 0.2 { return "Weak" }
        return "None"
    }

    private var correlationColor: Color {
        guard let r = analytics.correlationR else { return .gray }
        let absR = abs(r)
        if absR > 0.7 { return LiquidGlass.redCritical }
        if absR > 0.4 { return LiquidGlass.amberWarning }
        return LiquidGlass.greenPositive
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 14) {
            // Header
            HStack(spacing: 10) {
                Image(systemName: "chart.xyaxis.line")
                    .font(.title3)
                    .foregroundColor(LiquidGlass.tealPrimary)
                Text("HR vs PM2.5 Correlation")
                    .font(.subheadline.weight(.medium))
                    .foregroundColor(.white)
                Spacer()
                if let r = analytics.correlationR {
                    Text("r = \(String(format: "%.2f", r))")
                        .font(.caption.weight(.bold).monospacedDigit())
                        .foregroundColor(correlationColor)
                        .padding(.horizontal, 8)
                        .padding(.vertical, 3)
                        .background(RoundedRectangle(cornerRadius: LiquidGlass.radiusChip).fill(correlationColor.opacity(0.15)))
                }
            }

            // Dual-axis chart
            if !analytics.heartRateSeries.isEmpty || !analytics.pm25Series.isEmpty {
                Chart {
                    ForEach(analytics.heartRateSeries, id: \.hour) { point in
                        LineMark(
                            x: .value("Hour", point.hour),
                            y: .value("HR", point.value),
                            series: .value("Series", "Heart Rate")
                        )
                        .foregroundStyle(LiquidGlass.redCritical)
                        .interpolationMethod(.catmullRom)
                    }

                    ForEach(analytics.pm25Series, id: \.hour) { point in
                        LineMark(
                            x: .value("Hour", point.hour),
                            y: .value("PM2.5", point.value),
                            series: .value("Series", "PM2.5")
                        )
                        .foregroundStyle(LiquidGlass.amberWarning)
                        .interpolationMethod(.catmullRom)
                        .lineStyle(StrokeStyle(lineWidth: 2, dash: [5, 3]))
                    }
                }
                .chartXAxisLabel("Hour")
                .chartForegroundStyleScale([
                    "Heart Rate": LiquidGlass.redCritical,
                    "PM2.5": LiquidGlass.amberWarning,
                ])
                .chartXAxis {
                    AxisMarks(values: .stride(by: 4)) { value in
                        AxisValueLabel {
                            if let hour = value.as(Int.self) {
                                Text("\(hour)h")
                                    .font(.caption2)
                                    .foregroundColor(.white.opacity(0.4))
                            }
                        }
                        AxisGridLine()
                            .foregroundStyle(.white.opacity(0.1))
                    }
                }
                .chartYAxis {
                    AxisMarks { _ in
                        AxisGridLine()
                            .foregroundStyle(.white.opacity(0.1))
                        AxisValueLabel()
                            .foregroundStyle(.white.opacity(0.4))
                    }
                }
                .frame(height: 180)
            }

            // Footnote
            Text(analytics.footnote)
                .font(.caption2)
                .foregroundColor(.white.opacity(0.4))

            // Correlation strength label
            HStack(spacing: 6) {
                Circle()
                    .fill(correlationColor)
                    .frame(width: 8, height: 8)
                Text("\(correlationText) correlation")
                    .font(.caption)
                    .foregroundColor(.white.opacity(0.6))
            }
        }
        .glassCard()
    }
}
