import SwiftUI

// Ensures the view is part of the target even if Xcode grouping lags.
// (File inclusion is managed by project.pbxproj.)

struct DashboardView: View {
    @Binding var selectedTab: Int
    @StateObject private var viewModel: DashboardViewModel
    @EnvironmentObject private var container: AppContainer
    @EnvironmentObject private var esp32MinuteService: Esp32MinuteVitalsPersistenceService

    init(selectedTab: Binding<Int>, viewModel: DashboardViewModel) {
        self._selectedTab = selectedTab
        self._viewModel = StateObject(wrappedValue: viewModel)
    }

    private var latestHR: Double {
        if esp32MinuteService.isBleConnected && esp32MinuteService.latestLiveHeartRate > 0 {
            return esp32MinuteService.latestLiveHeartRate
        }

        if minuteAverageHR > 0 {
            return minuteAverageHR
        }

        return latestPersistedHR
    }

    private var latestSpO2: Double {
        if esp32MinuteService.isBleConnected && esp32MinuteService.latestLiveSpO2 > 0 {
            return esp32MinuteService.latestLiveSpO2
        }

        if minuteAverageSpO2 > 0 {
            return minuteAverageSpO2
        }

        return latestPersistedSpO2
    }

    private var latestPersistedHR: Double {
        viewModel.snapshot?.recentVitals
            .first(where: { $0.type == .heartRate && !isEsp32MinuteAverageSource($0.source) })?
            .value ?? 0
    }

    private var latestPersistedSpO2: Double {
        viewModel.snapshot?.recentVitals
            .first(where: { $0.type == .spO2 && !isEsp32MinuteAverageSource($0.source) })?
            .value ?? 0
    }

    private var persistedMinuteAverageHR: Double {
        viewModel.snapshot?.recentVitals
            .first(where: { $0.type == .heartRate && isEsp32MinuteAverageSource($0.source) })?
            .value ?? 0
    }

    private var persistedMinuteAverageSpO2: Double {
        viewModel.snapshot?.recentVitals
            .first(where: { $0.type == .spO2 && isEsp32MinuteAverageSource($0.source) })?
            .value ?? 0
    }

    private var minuteAverageHR: Double {
        if esp32MinuteService.latestMinuteAverageHeartRate > 0 {
            return esp32MinuteService.latestMinuteAverageHeartRate
        }

        return persistedMinuteAverageHR
    }

    private var minuteAverageSpO2: Double {
        if esp32MinuteService.latestMinuteAverageSpO2 > 0 {
            return esp32MinuteService.latestMinuteAverageSpO2
        }

        return persistedMinuteAverageSpO2
    }

    private var latestSteps: Double {
        let steps = (viewModel.snapshot?.recentVitals ?? []).filter { $0.type == .steps }
        guard !steps.isEmpty else { return 0 }

        // Steps are stored as one row per local day in SQLite (timestamp = local day start).
        // Still, for safety (older DBs), compute "today" as the local day bucket.
        let calendar = Calendar.current
        let todayStart = calendar.startOfDay(for: Date())

        let todays = steps.filter { calendar.isDate($0.timestamp, inSameDayAs: todayStart) }
        if let maxToday = todays.map(\.value).max(), maxToday > 0 {
            return maxToday
        }

        // Fallback: show the latest available day.
        guard let latestTs = steps.map(\.timestamp).max() else { return 0 }
        return steps
            .filter { $0.timestamp == latestTs }
            .map(\.value)
            .max() ?? 0
    }

    private var isHrLiveFromEsp: Bool {
        esp32MinuteService.isBleConnected && esp32MinuteService.latestLiveHeartRate > 0
    }

    private var isSpO2LiveFromEsp: Bool {
        esp32MinuteService.isBleConnected && esp32MinuteService.latestLiveSpO2 > 0
    }

    private var mostRecentSleepSession: SleepSessionInfo? {
        viewModel.snapshot?.sleepSessions.max(by: { $0.endTime < $1.endTime })
    }

    private var sleepMinutes: Int {
        mostRecentSleepSession?.durationMinutes ?? 0
    }

    private var sleepQuality: Double {
        mostRecentSleepSession?.qualityScore ?? 0
    }

    private var hasProfile: Bool {
        viewModel.snapshot?.patientProfile != nil
    }

    private func isEsp32MinuteAverageSource(_ source: String) -> Bool {
        source.caseInsensitiveCompare(Esp32MinuteVitalsPersistenceService.averageSource) == .orderedSame
    }

    var body: some View {
        ZStack(alignment: .top) {
            ScrollView(showsIndicators: false) {
                VStack(spacing: 0) {
                    Spacer().frame(height: 80)

                    if hasProfile {
                        HomeWidgetGrid(
                            heartRateLatest: latestHR,
                            heartRateAverage: minuteAverageHR,
                            spO2Latest: latestSpO2,
                            spO2Average: minuteAverageSpO2,
                            steps: latestSteps,
                            envReading: viewModel.snapshot?.environmentReading,
                            sleepMinutes: sleepMinutes,
                            sleepQuality: sleepQuality,
                            coachingAdvice: viewModel.snapshot?.coachingAdvice,
                            hasProfile: hasProfile,
                            isHeartRateLive: isHrLiveFromEsp,
                            isSpO2Live: isSpO2LiveFromEsp,
                            selectedTab: $selectedTab
                        )
                        .padding(.horizontal, 16)
                        .padding(.bottom, 32)
                    } else {
                        NoPatientProfileHomeView {
                            selectedTab = 4
                            container.shouldPresentPatientProfileEdit = true
                        }
                        .padding(.bottom, 32)
                    }
                }
            }

            HomeTopBar(user: viewModel.snapshot?.user) {
                selectedTab = 4
            }
        }
        .pageEnterAnimation()
        .task {
            // Seed snapshot instantly from cache — prevents the "no profile" flash
            viewModel.preload(engine: container.engine)
            await viewModel.loadIfNeeded()

            Task {
                await viewModel.refreshCoachingAdvice(using: container.engine)
            }
        }
        .refreshable {
            await viewModel.load()

            Task {
                await viewModel.refreshCoachingAdvice(using: container.engine, forceRefresh: true)
            }
        }
        .onChange(of: container.engine.lastSyncCompletedAt) { oldValue, newValue in
            // Refresh dashboard snapshot when sync completes
            if newValue != nil {
                Task {
                    await viewModel.refresh()
                }
            }
        }
    }
}

