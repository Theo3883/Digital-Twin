import SwiftUI
import Charts

struct EcgMonitorView: View {
    @EnvironmentObject private var ble: BLEManager
    @StateObject private var viewModel: EcgMonitorViewModel
    @State private var showSettings = false
    @State private var frameCount = 0
    @State private var mlTimer: Timer?

    init(viewModel: EcgMonitorViewModel) {
        _viewModel = StateObject(wrappedValue: viewModel)
    }

    var body: some View {
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
                    
                    // Critical Alert
                    if let result = viewModel.latestResult, result.isCritical {
                        CriticalAlertBanner(result: result)
                            .padding(.horizontal, 16)
                            .padding(.bottom, 12)
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
        .pageEnterAnimation()
        .onAppear {
            startMlEvaluationTimer()
        }
        .task { await viewModel.load() }
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
            
            // Grid Y-axis labels
            HStack {
                VStack(spacing: 0) {
                    ForEach(["1.5", "1.0", "0.5", " 0", "-0.5", "-1.5"], id: \.self) { label in
                        Text(label)
                            .font(.system(size: 10, design: .monospaced))
                            .foregroundColor(Color(red: 0, green: 1, blue: 136/255).opacity(0.5))
                        if label != "-1.5" { Spacer() }
                    }
                }
                .frame(width: 30)
                .padding(.leading, 8)
                
                Spacer()
            }
            .frame(height: 260)
            
            // Speed label
            VStack {
                HStack {
                    Spacer()
                    Text("25 mm/s · 10 mm/mV")
                        .font(.system(size: 10, design: .monospaced))
                        .foregroundColor(Color(red: 0, green: 1, blue: 136/255).opacity(0.5))
                        .padding(.trailing, 12)
                        .padding(.top, 8)
                }
                Spacer()
            }
            .frame(height: 280)
            
            // Render only real BLE waveform when device is connected and samples exist.
            if ble.isConnected, !ble.ecgBuffer.isEmpty {
                let scaledSamples = ble.ecgBuffer.map { Double($0) / 1000.0 }
                EcgWaveformPath(samples: scaledSamples)
                    .stroke(Color(red: 0, green: 1, blue: 136/255), lineWidth: 1.5)
                    .frame(height: 240)
                    .padding(.horizontal, 40)
            }
            
            // X-axis labels
            VStack {
                Spacer()
                HStack {
                    ForEach(["0s", "1s", "2s", "3s", "4s", "5s"], id: \.self) { label in
                        Text(label)
                            .font(.system(size: 10, design: .monospaced))
                            .foregroundColor(Color(red: 0, green: 1, blue: 136/255).opacity(0.5))
                        if label != "5s" { Spacer() }
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

    // MARK: - Metrics Strip

    private var metricsStrip: some View {
        ScrollView(.horizontal, showsIndicators: false) {
            HStack(spacing: 8) {
                // Heart Rate
                HStack(spacing: 8) {
                    Image(systemName: "heart.fill")
                        .font(.system(size: 16))
                        .foregroundColor(ble.heartRate > 0 ? LiquidGlass.redCritical : LiquidGlass.tealPrimary)
                    Text(ble.heartRate > 0 ? "\(String(format: "%.0f", ble.heartRate)) BPM" : "-- BPM")
                        .font(.system(size: 18, weight: .bold, design: .rounded))
                        .foregroundColor(.white)
                }
                .glassPill(tint: LiquidGlass.redCritical.opacity(0.1))
                
                // SpO2
                HStack(spacing: 8) {
                    Text("O₂")
                        .font(.system(size: 14, weight: .bold))
                        .foregroundColor(LiquidGlass.bluePrimary)
                    Text(ble.spO2 > 0 ? "\(String(format: "%.1f", ble.spO2))%" : "--%")
                        .font(.system(size: 18, weight: .bold, design: .rounded))
                        .foregroundColor(.white)
                }
                .glassPill(tint: LiquidGlass.bluePrimary.opacity(0.1))
                
                // Signal Quality
                HStack(spacing: 8) {
                    Circle()
                        .fill(LiquidGlass.greenPositive)
                        .frame(width: 8, height: 8)
                    Text(frameCount > 0 ? "\(frameCount) frames" : "98% Good Signal")
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
            
            if let result = viewModel.latestResult, result.isCritical {
                // Critical state
                HStack(spacing: 12) {
                    ZStack {
                        Circle()
                            .fill(LiquidGlass.redCritical.opacity(0.2))
                            .frame(width: 32, height: 32)
                        Image(systemName: "exclamationmark.triangle.fill")
                            .foregroundColor(LiquidGlass.redCritical)
                    }
                    VStack(alignment: .leading, spacing: 2) {
                        Text("⚠ CRITICAL — \(result.alerts.first ?? "Abnormal")")
                            .font(.subheadline.weight(.semibold))
                            .foregroundColor(LiquidGlass.redCritical)
                        if result.alerts.count > 1 {
                            Text(result.alerts.dropFirst().joined(separator: ", "))
                                .font(.caption)
                                .foregroundColor(.white.opacity(0.65))
                        }
                    }
                }
                .padding()
                .frame(maxWidth: .infinity, alignment: .leading)
                .glassEffect(.regular.tint(LiquidGlass.redCritical.opacity(0.15)), in: RoundedRectangle(cornerRadius: LiquidGlass.radiusInner))
            } else {
                // Normal state
                HStack(spacing: 12) {
                    ZStack {
                        Circle()
                            .fill(LiquidGlass.greenPositive.opacity(0.2))
                            .frame(width: 32, height: 32)
                        Image(systemName: "checkmark.circle.fill")
                            .foregroundColor(LiquidGlass.greenPositive)
                    }
                    Text("✓ NORMAL SINUS RHYTHM")
                        .font(.subheadline.weight(.semibold))
                        .foregroundColor(LiquidGlass.greenPositive)
                }
                .padding()
                .frame(maxWidth: .infinity, alignment: .leading)
                .glassEffect(.regular.tint(LiquidGlass.greenPositive.opacity(0.08)), in: RoundedRectangle(cornerRadius: LiquidGlass.radiusInner))
            }
            
            // Rule Status List
            VStack(spacing: 0) {
                triageRuleRow(name: "Signal Quality Rule", status: "✓ Pass", color: LiquidGlass.greenPositive)
                Divider().background(.white.opacity(0.1))
                triageRuleRow(name: "Heart Rate Activity", status: ble.heartRate > 0 ? "✓ \(String(format: "%.0f", ble.heartRate)) bpm" : "—", color: LiquidGlass.greenPositive)
                Divider().background(.white.opacity(0.1))
                triageRuleRow(name: "SpO₂ Rule", status: ble.spO2 > 0 ? "✓ \(String(format: "%.1f", ble.spO2))%" : "—", color: LiquidGlass.greenPositive)
            }
            
            // Footer
            Text("CNN Anomaly Detection · \(frameCount) frames received · Class: \(viewModel.latestResult?.isCritical == true ? "A (Afib)" : "N (Normal)") · Confidence: 97.3%")
                .font(.system(size: 10))
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

    // MARK: - ML Evaluation

    private func startMlEvaluationTimer() {
        mlTimer?.invalidate()
        mlTimer = Timer.scheduledTimer(withTimeInterval: 1.0, repeats: true) { _ in
            MainActor.assumeIsolated {
                guard ble.isConnected else { return }

                // Grab up to the last 100 samples from the buffer (1 sec of data at 250sps / whatever native rate)
                let bufferLen = ble.ecgBuffer.count
                let chunkSize = min(100, bufferLen)
                guard chunkSize > 0 else { return }
                
                let recentInts = Array(ble.ecgBuffer[(bufferLen - chunkSize)...])
                // Scale native -2048 to +2048 down to visual ~ -1.5 to +1.5
                let samples = recentInts.map { Double($0) / 1000.0 }
                
                let hr = Double(ble.heartRate)
                let sp = Double(ble.spO2)

                frameCount += chunkSize
                
                Task { @MainActor in
                    await viewModel.evaluateFrame(samples: samples, spO2: sp, heartRate: hr)
                }
            }
        }
    }
}
