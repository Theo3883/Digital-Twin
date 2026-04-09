import SwiftUI
import Charts

struct EcgMonitorView: View {
    @EnvironmentObject var engineWrapper: MobileEngineWrapper
    @State private var ecgSamples: [Double] = []
    @State private var heartRate: Double = 0
    @State private var spO2: Double = 0
    @State private var latestResult: EcgEvaluationResult?
    @State private var isMonitoring = false

    var body: some View {
        NavigationView {
            ScrollView {
                VStack(spacing: 20) {
                    // Critical Alert Banner
                    if let result = latestResult, result.isCritical {
                        CriticalAlertBanner(result: result)
                    }

                    // Waveform Chart
                    EcgWaveformChart(samples: ecgSamples)

                    // Stats Cards
                    HStack(spacing: 12) {
                        EcgStatCard(title: "Heart Rate", value: String(format: "%.0f", heartRate), unit: "bpm", icon: "heart.fill", color: .red)
                        EcgStatCard(title: "SpO₂", value: String(format: "%.1f", spO2), unit: "%", icon: "lungs.fill", color: .cyan)
                    }

                    // Triage Status
                    if let result = latestResult {
                        TriageStatusCard(result: result)
                    }

                    // Monitor Control
                    Button(isMonitoring ? "Stop Monitoring" : "Start Monitoring") {
                        isMonitoring.toggle()
                        if isMonitoring { startSimulatedEcg() }
                    }
                    .frame(maxWidth: .infinity)
                    .frame(height: 50)
                    .liquidGlassButtonStyle()

                    Spacer(minLength: 100)
                }
                .padding()
            }
            .navigationTitle("ECG Monitor")
            .liquidGlassNavigationStyle()
        }
    }

    /// Simulate ECG data for demo — in production this comes from BLE / Apple Watch
    private func startSimulatedEcg() {
        Timer.scheduledTimer(withTimeInterval: 1.0, repeats: true) { timer in
            guard isMonitoring else { timer.invalidate(); return }

            // Generate simulated ECG waveform
            var samples: [Double] = []
            for i in 0..<100 {
                let t = Double(i) / 100.0
                let pWave = 0.15 * sin(2 * .pi * t * 5)
                let qrsComplex = (t > 0.4 && t < 0.5) ? 1.2 * sin(2 * .pi * (t - 0.4) * 20) : 0
                let tWave = 0.3 * sin(2 * .pi * (t - 0.6) * 3)
                samples.append(pWave + qrsComplex + tWave + Double.random(in: -0.05...0.05))
            }

            let hr = Double.random(in: 60...100)
            let sp = Double.random(in: 95...99)

            Task { @MainActor in
                ecgSamples = samples
                heartRate = hr
                spO2 = sp

                latestResult = await engineWrapper.evaluateEcgFrame(samples: samples, spO2: sp, heartRate: hr)
            }
        }
    }
}

// MARK: - ECG Waveform Chart

struct EcgWaveformChart: View {
    let samples: [Double]

    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            Text("ECG Waveform")
                .font(.caption).foregroundColor(.secondary)

            if samples.isEmpty {
                Text("No data — start monitoring")
                    .font(.subheadline).foregroundColor(.secondary)
                    .frame(maxWidth: .infinity, minHeight: 200)
            } else {
                Chart {
                    ForEach(Array(samples.enumerated()), id: \.offset) { index, value in
                        LineMark(
                            x: .value("Sample", index),
                            y: .value("mV", value)
                        )
                        .foregroundStyle(LiquidGlass.greenPositive.gradient)
                    }
                }
                .chartYScale(domain: -1.5...1.5)
                .chartXAxis(.hidden)
                .frame(height: 200)
            }
        }
        .glassCard()
    }
}

// MARK: - Stat Card

struct EcgStatCard: View {
    let title: String
    let value: String
    let unit: String
    let icon: String
    let color: Color

    var body: some View {
        VStack(spacing: 8) {
            Image(systemName: icon)
                .font(.title2).foregroundColor(color)
            Text(value)
                .font(.title).fontWeight(.bold)
            Text(unit)
                .font(.caption).foregroundColor(.secondary)
            Text(title)
                .font(.caption2).foregroundColor(.secondary)
        }
        .frame(maxWidth: .infinity)
        .glassCard(tint: color.opacity(0.2))
    }
}

// MARK: - Triage Status

struct TriageStatusCard: View {
    let result: EcgEvaluationResult

    private var tint: Color {
        switch result.triageResult {
        case 0: return LiquidGlass.greenPositive
        case 1: return LiquidGlass.amberWarning
        case 2: return LiquidGlass.redCritical
        default: return .gray
        }
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            HStack {
                Image(systemName: result.isCritical ? "exclamationmark.triangle.fill" :
                                  result.isWarning ? "exclamationmark.circle.fill" :
                                  "checkmark.circle.fill")
                    .foregroundColor(tint)
                Text("Triage: \(result.triageDisplay)")
                    .font(.headline)
            }

            if !result.alerts.isEmpty {
                ForEach(result.alerts, id: \.self) { alert in
                    Text("• \(alert)")
                        .font(.caption)
                        .foregroundColor(.secondary)
                }
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .glassCard(tint: tint.opacity(0.2))
    }
}

// MARK: - Critical Alert Banner

struct CriticalAlertBanner: View {
    let result: EcgEvaluationResult

    var body: some View {
        HStack {
            Image(systemName: "exclamationmark.triangle.fill")
                .font(.title2).foregroundColor(.white)
            VStack(alignment: .leading) {
                Text("Critical Alert")
                    .font(.headline).foregroundColor(.white)
                Text(result.alerts.first ?? "Abnormal reading detected")
                    .font(.caption).foregroundColor(.white.opacity(0.9))
            }
            Spacer()
        }
        .glassBanner(tint: LiquidGlass.redCritical)
    }
}
