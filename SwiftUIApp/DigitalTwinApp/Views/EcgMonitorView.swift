import SwiftUI

struct EcgMonitorView: View {
    @EnvironmentObject private var ble: BLEManager
    @StateObject private var viewModel: EcgMonitorViewModel
    @State private var showSettings = false

    init(viewModel: EcgMonitorViewModel) {
        _viewModel = StateObject(wrappedValue: viewModel)
    }

    var body: some View {
        ZStack {
            // Ensure the ECG tab matches the rest of the app even when wrapped
            // in a NavigationStack (which can otherwise draw its own background).
            MeshGradientBackground()

            ScrollView(showsIndicators: false) {
                VStack(spacing: 0) {
                    if !viewModel.hasProfile {
                        noProfileWarning
                    } else {
                        // Top Bar
                        ecgTopBar

                        // Connection Panel
                        if showSettings {
                            connectionPanel
                        }

                        // ECG Canvas
                        ecgCanvasView

                        // Metrics Strip
                        metricsStrip

                        // Triage Panel
                        triagePanel
                    }

                    Spacer(minLength: 100)
                }
            }
            .background(Color.clear)
        }
        .pageEnterAnimation()
        .task { await viewModel.load() }
        .onChange(of: ble.isConnected) { _, connected in
            // UI-only state: reflect connection status in triage panel.
            // The actual evaluation loop is owned by BackgroundECGTriageService.
            if connected {
                viewModel.reconnectTriage()
            } else {
                viewModel.disconnectTriage()
            }
        }
        .onAppear {
            if ble.isConnected {
                viewModel.reconnectTriage()
            } else {
                viewModel.disconnectTriage()
            }
        }
        .liquidGlassNavigationStyle()
    }

    // MARK: - No Profile Warning

    private var noProfileWarning: some View {
        VStack(spacing: 16) {
            ZStack {
                Circle()
                    .fill(LiquidGlass.redCritical.opacity(0.15))
                    .frame(width: 64, height: 64)
                Image(systemName: "person.crop.circle")
                    .font(.system(size: 32))
                    .foregroundColor(LiquidGlass.redCritical)
            }
            Text("Patient profile required")
                .font(.system(size: 20, weight: .bold))
                .foregroundColor(.white)
            Text("Create a patient profile to use ECG monitoring")
                .font(.subheadline)
                .foregroundColor(.white.opacity(0.65))
                .multilineTextAlignment(.center)
            Button("Create patient profile") {}
                .liquidGlassButtonStyle()
        }
        .frame(maxWidth: 360)
        .padding(.top, 120)
        .padding(.horizontal, 24)
        .glassCard()
    }

    // MARK: - Top Bar

    private var ecgTopBar: some View {
        HStack {
            Text("ECG Monitor")
                .font(.system(size: 20, weight: .semibold))
                .foregroundColor(.white)

            Spacer()

            // Connection status pill
            HStack(spacing: 8) {
                Circle()
                    .fill(ble.isConnected ? LiquidGlass.greenPositive :
                            (viewModel.latestResult?.isCritical == true ? LiquidGlass.redCritical : .gray))
                    .frame(width: 8, height: 8)

                Text(ble.isConnected ? "Recording · ESP32" : "Not Connected")
                    .font(.caption)
                    .foregroundColor(.white.opacity(0.65))

                Button(action: { showSettings.toggle() }) {
                    Image(systemName: "gearshape.fill")
                        .font(.caption)
                        .foregroundColor(.white.opacity(0.5))
                }
            }
            .glassPill()
        }
        .padding(.horizontal, 16)
        .padding(.vertical, 12)
    }

    // MARK: - Connection Panel

    private var connectionPanel: some View {
        VStack(spacing: 12) {
            HStack {
                Text(ble.isConnected ? "Connected to ESP32" : (ble.isScanning ? "Scanning for ESP32..." : "Bluetooth Ready"))
                    .font(.system(size: 14))
                    .foregroundColor(.white)
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .padding(.leading, 10)

                Button(ble.isConnected ? "Disconnect" : (ble.isScanning ? "Stop Scan" : "Connect ESP32")) {
                    if ble.isConnected {
                        ble.disconnect()
                    } else if ble.isScanning {
                        ble.stopScanning()
                    } else {
                        ble.startScanning()
                    }
                }
                .buttonStyle(.glass)
            }
        }
        .padding(.horizontal, 16)
        .padding(.bottom, 12)
    }

    // MARK: - ECG Canvas

    private var ecgCanvasView: some View {
        ZStack {
            // Dark background
            RoundedRectangle(cornerRadius: 0)
                .fill(Color(red: 8/255, green: 14/255, blue: 30/255).opacity(0.95))
                .frame(height: 280)

            GeometryReader { geo in
                let gridHeight: CGFloat = 240
                let gridTopInset: CGFloat = (280 - gridHeight) / 2
                let gridRect = CGRect(x: 0, y: gridTopInset, width: geo.size.width, height: gridHeight)

                // Draw a calibrated grid + labels using the SAME transform as the waveform.
                EcgCalibratedGrid(rect: gridRect, yRangeMv: yRangeMv)

                // Calibration label
                VStack {
                    HStack {
                        Spacer()
                        Text(calibrationLabelText)
                            .font(.system(size: 10, design: .monospaced))
                            .foregroundColor(Color(red: 0, green: 1, blue: 136/255).opacity(0.5))
                            .padding(.trailing, 12)
                            .padding(.top, 8)
                    }
                    Spacer()
                }
                .frame(height: 280)

                if ble.isConnected, !ble.ecgBuffer.isEmpty {
                    let leadIISamples = ble.leadIIBuffer
                    if !leadIISamples.isEmpty {
                        let recentSamples = leadIISamples.suffix(displaySampleCount)
                        let scaledSamples = recentSamples.map { Double($0) / adcScaleDivisor }

                        // Robust baseline: median of middle 80%
                        let baseline = robustBaseline(scaledSamples)
                        let centeredSamples = scaledSamples.map { $0 - baseline }

                        EcgWaveformPath(samples: centeredSamples, yRangeMv: yRangeMv)
                            .stroke(Color(red: 0, green: 1, blue: 136/255), lineWidth: 1.5)
                            .frame(height: 240)
                            .padding(.horizontal, 40)
                            .offset(y: gridTopInset)
                    }
                }

                // X-axis labels
                VStack {
                    Spacer()
                    HStack {
                        ForEach(xAxisLabels, id: \.self) { label in
                            Text(label)
                                .font(.system(size: 10, design: .monospaced))
                                .foregroundColor(Color(red: 0, green: 1, blue: 136/255).opacity(0.5))
                            if label != xAxisLabels.last { Spacer() }
                        }
                    }
                    .padding(.horizontal, 40)
                    .padding(.bottom, 4)
                }
                .frame(height: 280)
            }
            .overlay(
                Rectangle()
                    .stroke(Color.white.opacity(0.1), lineWidth: 1)
            )
        }
    }

    // MARK: - Metrics Strip

    private var metricsStrip: some View {
        ScrollView(.horizontal, showsIndicators: false) {
            HStack(spacing: 8) {
                // Heart Rate
                HStack(spacing: 8) {
                    Image(systemName: "heart.fill")
                        .font(.system(size: 16))
                        .foregroundColor(ble.isConnected && ble.heartRate > 0 ? LiquidGlass.redCritical : LiquidGlass.tealPrimary)
                    Text(ble.isConnected && ble.heartRate > 0 ? "\(String(format: "%.0f", ble.heartRate)) BPM" : "-- BPM")
                        .font(.system(size: 18, weight: .bold, design: .rounded))
                        .foregroundColor(.white)
                }
                .glassPill(tint: ble.isConnected && ble.heartRate > 0 ? LiquidGlass.redCritical.opacity(0.1) : LiquidGlass.tealPrimary.opacity(0.1))

                // SpO2
                HStack(spacing: 8) {
                    Text("O₂")
                        .font(.system(size: 14, weight: .bold))
                        .foregroundColor(LiquidGlass.bluePrimary)
                    Text(ble.isConnected && ble.spO2 > 0 ? "\(String(format: "%.1f", ble.spO2))%" : "--%")
                        .font(.system(size: 18, weight: .bold, design: .rounded))
                        .foregroundColor(.white)
                }
                .glassPill(tint: LiquidGlass.bluePrimary.opacity(0.1))

                // Signal Quality
                HStack(spacing: 8) {
                    Circle()
                        .fill(viewModel.latestResult?.signalQualityPassed == true ? LiquidGlass.greenPositive : LiquidGlass.redCritical)
                        .frame(width: 8, height: 8)
                    Text(viewModel.latestResult?.signalQualityPassed == true ? "✓ Good Signal" : "✗ Weak Signal")
                        .font(.system(size: 14))
                        .foregroundColor(.white.opacity(0.65))
                }
                .glassPill()
            }
            .padding(.horizontal, 16)
            .padding(.vertical, 12)
        }
    }

    // MARK: - Triage Panel

    private var triagePanel: some View {
        VStack(alignment: .leading, spacing: 12) {
            Text("AI Triage Engine · Status")
                .font(.system(size: 18, weight: .semibold))
                .foregroundColor(.white)

            if let mlError = viewModel.mlLoadError {
                // Model failed to load
                HStack(spacing: 12) {
                    ZStack {
                        Circle()
                            .fill(LiquidGlass.redCritical.opacity(0.2))
                            .frame(width: 32, height: 32)
                        Image(systemName: "xmark.octagon.fill")
                            .foregroundColor(LiquidGlass.redCritical)
                    }
                    VStack(alignment: .leading, spacing: 2) {
                        Text("AI Triage Offline")
                            .font(.subheadline.weight(.semibold))
                            .foregroundColor(LiquidGlass.redCritical)
                        Text(mlError)
                            .font(.caption)
                            .foregroundColor(.white.opacity(0.65))
                    }
                }
                .padding()
                .frame(maxWidth: .infinity, alignment: .leading)
                .glassEffect(.regular.tint(LiquidGlass.redCritical.opacity(0.15)), in: RoundedRectangle(cornerRadius: LiquidGlass.radiusInner))

            } else if !ble.isConnected {
                // Disconnected
                HStack(spacing: 12) {
                    ZStack {
                        Circle()
                            .fill(.yellow.opacity(0.2))
                            .frame(width: 32, height: 32)
                        Image(systemName: "antenna.radiowaves.left.and.right.slash")
                            .foregroundColor(.yellow)
                    }
                    VStack(alignment: .leading, spacing: 2) {
                        Text("ESP32 Disconnected")
                            .font(.subheadline.weight(.semibold))
                            .foregroundColor(.yellow)
                        Text("Monitoring paused. Connect device to resume.")
                            .font(.caption)
                            .foregroundColor(.white.opacity(0.65))
                    }
                }
                .padding()
                .frame(maxWidth: .infinity, alignment: .leading)
                .glassEffect(.regular.tint(.yellow.opacity(0.1)), in: RoundedRectangle(cornerRadius: LiquidGlass.radiusInner))

            } else if let result = viewModel.latestResult, result.isCritical {
                // Critical
                HStack(spacing: 12) {
                    ZStack {
                        Circle()
                            .fill(LiquidGlass.redCritical.opacity(0.2))
                            .frame(width: 32, height: 32)
                        Image(systemName: "exclamationmark.triangle.fill")
                            .foregroundColor(LiquidGlass.redCritical)
                    }
                    VStack(alignment: .leading, spacing: 2) {
                        Text("⚠ CRITICAL — \(result.mlTopLabel ?? result.alerts.first ?? "Abnormal")")
                            .font(.subheadline.weight(.semibold))
                            .foregroundColor(LiquidGlass.redCritical)
                        if !result.alerts.isEmpty {
                            Text(result.alerts.joined(separator: ", "))
                                .font(.caption)
                                .foregroundColor(.white.opacity(0.65))
                        }
                    }
                }
                .padding()
                .frame(maxWidth: .infinity, alignment: .leading)
                .glassEffect(.regular.tint(LiquidGlass.redCritical.opacity(0.15)), in: RoundedRectangle(cornerRadius: LiquidGlass.radiusInner))

            } else if viewModel.latestResult != nil {
                // Normal
                HStack(spacing: 12) {
                    ZStack {
                        Circle()
                            .fill(LiquidGlass.greenPositive.opacity(0.2))
                            .frame(width: 32, height: 32)
                        Image(systemName: "checkmark.circle.fill")
                            .foregroundColor(LiquidGlass.greenPositive)
                    }
                    VStack(alignment: .leading, spacing: 2) {
                        Text("✓ \(viewModel.latestResult?.mlTopLabel?.uppercased() ?? "NORMAL SINUS RHYTHM")")
                            .font(.subheadline.weight(.semibold))
                            .foregroundColor(LiquidGlass.greenPositive)
                        if let conf = viewModel.latestResult?.mlConfidence {
                            Text("CNN Confidence: \(String(format: "%.0f", conf * 100))%")
                                .font(.caption)
                                .foregroundColor(.white.opacity(0.65))
                        }
                    }
                }
                .padding()
                .frame(maxWidth: .infinity, alignment: .leading)
                .glassEffect(.regular.tint(LiquidGlass.greenPositive.opacity(0.08)), in: RoundedRectangle(cornerRadius: LiquidGlass.radiusInner))
            } else {
                // Buffering
                HStack(spacing: 12) {
                    ProgressView()
                        .tint(.white)
                    Text("Buffering ECG Data...")
                        .font(.subheadline.weight(.semibold))
                        .foregroundColor(.white)
                }
                .padding()
                .frame(maxWidth: .infinity, alignment: .leading)
                .glassEffect(.regular.tint(.white.opacity(0.05)), in: RoundedRectangle(cornerRadius: LiquidGlass.radiusInner))
            }

            // Rule Status List
            VStack(spacing: 0) {
                triageRuleRow(
                    name: "Signal Quality Rule",
                    status: viewModel.latestResult?.signalQualityPassed == true ? "✓ Pass" : (ble.isConnected ? "✗ Fail" : "—"),
                    color: viewModel.latestResult?.signalQualityPassed == true ? LiquidGlass.greenPositive : (ble.isConnected ? LiquidGlass.redCritical : .gray)
                )
                Divider().background(.white.opacity(0.1))
                triageRuleRow(
                    name: "Heart Rate Activity",
                    status: ble.isConnected && ble.heartRate > 0 ? "✓ \(String(format: "%.0f", ble.heartRate)) bpm" : "—",
                    color: ble.isConnected && ble.heartRate > 0 ? LiquidGlass.greenPositive : .gray
                )
                Divider().background(.white.opacity(0.1))
                triageRuleRow(
                    name: "SpO₂ Rule",
                    status: ble.isConnected && ble.spO2 > 0 ? "✓ \(String(format: "%.1f", ble.spO2))%" : "—",
                    color: ble.isConnected && ble.spO2 > 0 ? LiquidGlass.greenPositive : .gray
                )
            }

            // Footer
            let label = viewModel.latestResult?.mlTopLabel ?? "--"
            let conf = viewModel.latestResult?.mlConfidence.map { String(format: "%.0f%%", $0 * 100) } ?? "--"
            Text("XceptionTime (ONNX) · \(viewModel.frameCount) evals · Class: \(label) · Confidence: \(conf)")
                .font(.system(size: 10, design: .monospaced))
                .foregroundColor(.white.opacity(0.3))
        }
        .padding(16)
        .glassCard()
        .padding(.horizontal, 16)
    }

    private func triageRuleRow(name: String, status: String, color: Color) -> some View {
        HStack {
            Text(name)
                .font(.caption)
                .foregroundColor(.white.opacity(0.65))
            Spacer()
            Text(status)
                .font(.caption.weight(.medium))
                .foregroundColor(color)
        }
        .padding(.vertical, 8)
    }

    // MARK: - ECG Calibration / Baseline

    private let sampleRateHz: Double = 100
    private let displaySampleCount: Int = 1000
    private let adcScaleDivisor: Double = 800.0

    /// Visible mV span for the waveform/grid mapping.
    private let yRangeMv: ClosedRange<Double> = (-1.5)...(1.5)

    private var displayedSeconds: Double {
        Double(displaySampleCount) / sampleRateHz
    }

    private var calibrationLabelText: String {
        let spanMv = yRangeMv.upperBound - yRangeMv.lowerBound
        return String(format: "%.0f Hz · %.1fs · %.1f mV span", sampleRateHz, displayedSeconds, spanMv)
    }

    private var xAxisLabels: [String] {
        let seconds = Int(floor(displayedSeconds))
        return (0...max(seconds, 1)).map { "\($0)s" }
    }

    private func robustBaseline(_ values: [Double]) -> Double {
        guard !values.isEmpty else { return 0 }
        let sorted = values.sorted()
        let n = sorted.count
        let low = Int(Double(n) * 0.10)
        let high = Int(Double(n) * 0.90)
        if high <= low { return sorted[n / 2] }
        let middle = Array(sorted[low..<high])
        return middle[middle.count / 2]
    }
}

private struct EcgCalibratedGrid: View {
    let rect: CGRect
    let yRangeMv: ClosedRange<Double>

    private let labelColor = Color(red: 0, green: 1, blue: 136/255).opacity(0.5)
    private let gridLineColor = Color(red: 0, green: 1, blue: 136/255).opacity(0.12)
    private let zeroLineColor = Color(red: 0, green: 1, blue: 136/255).opacity(0.30)

    private func y(forMv mv: Double) -> CGFloat {
        let minV = yRangeMv.lowerBound
        let maxV = yRangeMv.upperBound
        let t = (maxV - mv) / (maxV - minV) // 0 at top, 1 at bottom
        return rect.minY + CGFloat(t) * rect.height
    }

    var body: some View {
        let ticks: [Double] = [1.5, 1.0, 0.5, 0.0, -0.5, -1.0, -1.5]

        ZStack(alignment: .topLeading) {
            ForEach(ticks, id: \.self) { tick in
                let yPos = y(forMv: tick)
                Path { p in
                    p.move(to: CGPoint(x: rect.minX + 40, y: yPos))
                    p.addLine(to: CGPoint(x: rect.maxX - 12, y: yPos))
                }
                .stroke(tick == 0 ? zeroLineColor : gridLineColor, lineWidth: tick == 0 ? 1.0 : 0.5)
                .allowsHitTesting(false)

                Text(tick == 0 ? "0" : String(format: "%.1f", tick))
                    .font(.system(size: 10, design: .monospaced))
                    .foregroundColor(labelColor)
                    .frame(width: 34, alignment: .trailing)
                    .position(x: rect.minX + 20, y: yPos)
                    .allowsHitTesting(false)
            }
        }
        .frame(height: rect.maxY)
    }
}
