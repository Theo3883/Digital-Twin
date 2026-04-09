import SwiftUI

struct SleepView: View {
    @EnvironmentObject var engineWrapper: MobileEngineWrapper
    @State private var showingAddSheet = false
    @State private var selectedRange = DateRange.week

    private var latestSession: SleepSessionInfo? {
        engineWrapper.sleepSessions.first
    }

    private var averageDuration: Int {
        guard !engineWrapper.sleepSessions.isEmpty else { return 0 }
        return engineWrapper.sleepSessions.map(\.durationMinutes).reduce(0, +) / engineWrapper.sleepSessions.count
    }

    private var averageQuality: Double? {
        let scores = engineWrapper.sleepSessions.compactMap(\.qualityScore)
        guard !scores.isEmpty else { return nil }
        return scores.reduce(0, +) / Double(scores.count)
    }

    var body: some View {
        NavigationView {
            ScrollView {
                VStack(spacing: 20) {
                    // Sleep Summary Hero
                    if let latest = latestSession {
                        SleepHeroCard(session: latest)
                    }

                    // Stats Row
                    HStack(spacing: 12) {
                        SleepStatTile(title: "Avg Duration", value: formatDuration(averageDuration), icon: "clock.fill")
                        SleepStatTile(title: "Avg Quality", value: averageQuality.map { String(format: "%.0f%%", $0) } ?? "N/A", icon: "star.fill")
                        SleepStatTile(title: "Sessions", value: "\(engineWrapper.sleepSessions.count)", icon: "list.bullet")
                    }

                    // Date Range Picker
                    DateRangePickerView(selectedRange: $selectedRange)
                        .onChange(of: selectedRange) { _, _ in
                            Task { await loadData() }
                        }

                    // Sessions List
                    if engineWrapper.sleepSessions.isEmpty {
                        EmptySleepView()
                    } else {
                        VStack(alignment: .leading, spacing: 12) {
                            Text("Sleep History")
                                .glassSectionHeader()

                            ForEach(engineWrapper.sleepSessions) { session in
                                SleepSessionRow(session: session)
                            }
                        }
                    }

                    Spacer(minLength: 100)
                }
                .padding()
            }
            .navigationTitle("Sleep")
            .liquidGlassNavigationStyle()
            .toolbar {
                ToolbarItem(placement: .navigationBarTrailing) {
                    Button(action: { showingAddSheet = true }) {
                        Image(systemName: "plus")
                    }
                    .liquidGlassButtonStyle()
                }
            }
            .sheet(isPresented: $showingAddSheet) {
                AddSleepSessionSheet()
            }
            .task { await loadData() }
            .refreshable { await loadData() }
        }
    }

    private func loadData() async {
        let (from, to) = selectedRange.dateRange
        await engineWrapper.loadSleepSessions(from: from, to: to)
    }

    private func formatDuration(_ minutes: Int) -> String {
        let h = minutes / 60
        let m = minutes % 60
        return h > 0 ? "\(h)h \(m)m" : "\(m)m"
    }
}

// MARK: - Hero Card

struct SleepHeroCard: View {
    let session: SleepSessionInfo

    var body: some View {
        VStack(spacing: 12) {
            Image(systemName: "bed.double.fill")
                .font(.system(size: 40)).foregroundColor(.white)

            Text("Last Night")
                .font(.subheadline).foregroundColor(.white.opacity(0.8))

            Text(session.durationFormatted)
                .font(.largeTitle).fontWeight(.bold).foregroundColor(.white)

            Text(session.qualityDisplay)
                .font(.headline).foregroundColor(.white.opacity(0.9))

            Text(session.startTime.formatted(date: .abbreviated, time: .shortened))
                .font(.caption).foregroundColor(.white.opacity(0.7))
        }
        .frame(maxWidth: .infinity)
        .glassHeroCard(tint: LiquidGlass.purpleSleep)
    }
}

// MARK: - Stat Tile

struct SleepStatTile: View {
    let title: String
    let value: String
    let icon: String

    var body: some View {
        VStack(spacing: 6) {
            Image(systemName: icon)
                .foregroundColor(LiquidGlass.purpleSleep)
            Text(value)
                .font(.headline).fontWeight(.semibold)
            Text(title)
                .font(.caption2).foregroundColor(.secondary)
        }
        .frame(maxWidth: .infinity)
        .glassCard(tint: LiquidGlass.purpleSleep.opacity(0.15))
    }
}

// MARK: - Session Row

struct SleepSessionRow: View {
    let session: SleepSessionInfo

    var body: some View {
        HStack {
            Image(systemName: "moon.fill")
                .foregroundColor(LiquidGlass.purpleSleep)

            VStack(alignment: .leading, spacing: 2) {
                Text(session.startTime.formatted(date: .abbreviated, time: .omitted))
                    .font(.subheadline).fontWeight(.medium)
                Text("\(session.startTime.formatted(date: .omitted, time: .shortened)) – \(session.endTime.formatted(date: .omitted, time: .shortened))")
                    .font(.caption).foregroundColor(.secondary)
            }

            Spacer()

            VStack(alignment: .trailing, spacing: 2) {
                Text(session.durationFormatted)
                    .font(.subheadline).fontWeight(.semibold)
                Text(session.qualityDisplay)
                    .font(.caption).foregroundColor(.secondary)
            }
        }
        .glassCard()
    }
}

// MARK: - Add Sleep Session Sheet

struct AddSleepSessionSheet: View {
    @EnvironmentObject var engineWrapper: MobileEngineWrapper
    @Environment(\.dismiss) private var dismiss
    @State private var bedtime = Calendar.current.date(byAdding: .hour, value: -8, to: Date()) ?? Date()
    @State private var wakeTime = Date()
    @State private var qualityScore: Double = 70
    @State private var isSaving = false

    var body: some View {
        NavigationView {
            Form {
                Section("Sleep Times") {
                    DatePicker("Bedtime", selection: $bedtime)
                    DatePicker("Wake Time", selection: $wakeTime)
                }
                Section("Quality") {
                    VStack {
                        Text("Sleep Quality: \(Int(qualityScore))%")
                        Slider(value: $qualityScore, in: 0...100, step: 5)
                    }
                }
            }
            .navigationTitle("Log Sleep")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .cancellationAction) { Button("Cancel") { dismiss() } }
                ToolbarItem(placement: .confirmationAction) {
                    Button("Save") {
                        Task {
                            isSaving = true
                            let _ = await engineWrapper.recordSleepSession(startTime: bedtime, endTime: wakeTime, qualityScore: qualityScore)
                            dismiss()
                        }
                    }
                    .disabled(isSaving)
                    .liquidGlassButtonStyle()
                }
            }
        }
    }
}

// MARK: - Empty State

struct EmptySleepView: View {
    var body: some View {
        VStack(spacing: 16) {
            Image(systemName: "moon.zzz.fill")
                .font(.system(size: 50)).foregroundColor(.secondary)
            Text("No Sleep Data")
                .font(.title3).fontWeight(.semibold)
            Text("Log your sleep to track patterns and improve your rest.")
                .font(.subheadline).foregroundColor(.secondary).multilineTextAlignment(.center)
        }
        .frame(maxWidth: .infinity)
        .padding(.vertical, 60)
    }
}
