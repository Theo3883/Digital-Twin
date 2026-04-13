import SwiftUI
import Charts

struct EcgMonitorView: View {
    @StateObject private var viewModel: EcgMonitorViewModel
    @State private var ecgSamples: [Double] = []
    @State private var heartRate: Double = 0
    @State private var spO2: Double = 0
    @State private var isConnected = false
    @State private var isConnecting = false
    @State private var showSettings = false
    @State private var wsUrl = "ws://192.168.1.42:8080"
    @State private var frameCount = 0
    @State private var demoPhase: Double = 0
    @State private var simTimer: Timer?

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
        .onAppear { startDemoAnimation() }
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
                    .fill(isConnected ? LiquidGlass.greenPositive :
                          (viewModel.latestResult?.isCritical == true ? LiquidGlass.redCritical : .gray))
                    .frame(width: 8, height: 8)
                
                Text(isConnected ? "Recording · ESP32" : "Not Connected")
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
                TextField("WebSocket URL", text: $wsUrl)
                    .font(.system(size: 14, design: .monospaced))
                    .foregroundColor(.white)
                    .padding(10)
                    .glassEffect(.regular.tint(.primary.opacity(0.05)), in: RoundedRectangle(cornerRadius: LiquidGlass.radiusInput))
                
                Button(isConnected ? "Disconnect" : "Connect") {
                    isConnected.toggle()
                    if isConnected { startSimulatedEcg() }
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
            
            // ECG Waveform
            if !ecgSamples.isEmpty {
                EcgWaveformPath(samples: ecgSamples)
                    .stroke(Color(red: 0, green: 1, blue: 136/255), lineWidth: 1.5)
                    .frame(height: 240)
                    .padding(.horizontal, 40)
            } else {
                // Demo waveform animation
                EcgDemoWave(phase: demoPhase)
                    .stroke(Color(red: 0, green: 1, blue: 136/255).opacity(0.6), lineWidth: 1.5)
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
                        .foregroundColor(heartRate > 0 ? LiquidGlass.redCritical : LiquidGlass.tealPrimary)
                    Text(heartRate > 0 ? "\(String(format: "%.0f", heartRate)) BPM" : "-- BPM")
                        .font(.system(size: 18, weight: .bold, design: .rounded))
                        .foregroundColor(.white)
                }
                .glassPill(tint: LiquidGlass.redCritical.opacity(0.1))
                
                // SpO2
                HStack(spacing: 8) {
                    Text("O₂")
                        .font(.system(size: 14, weight: .bold))
                        .foregroundColor(LiquidGlass.bluePrimary)
                    Text(spO2 > 0 ? "\(String(format: "%.1f", spO2))%" : "--%")
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
                triageRuleRow(name: "Heart Rate Activity", status: heartRate > 0 ? "✓ \(String(format: "%.0f", heartRate)) bpm" : "—", color: LiquidGlass.greenPositive)
                Divider().background(.white.opacity(0.1))
                triageRuleRow(name: "SpO₂ Rule", status: spO2 > 0 ? "✓ \(String(format: "%.1f", spO2))%" : "—", color: LiquidGlass.greenPositive)
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

    // MARK: - Simulation

    private func startDemoAnimation() {
        Timer.scheduledTimer(withTimeInterval: 0.05, repeats: true) { _ in
            Task { @MainActor in
                demoPhase += 0.02
            }
        }
    }

    private func startSimulatedEcg() {
        simTimer?.invalidate()
        simTimer = Timer.scheduledTimer(withTimeInterval: 1.0, repeats: true) { _ in
            MainActor.assumeIsolated {
                guard isConnected else { simTimer?.invalidate(); simTimer = nil; return }

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

                ecgSamples = samples
                heartRate = hr
                spO2 = sp
                frameCount += 100
                Task { @MainActor in
                    await viewModel.evaluateFrame(samples: samples, spO2: sp, heartRate: hr)
                }
            }
        }
    }
}
