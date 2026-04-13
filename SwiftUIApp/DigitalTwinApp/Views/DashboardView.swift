import SwiftUI

// Ensures the view is part of the target even if Xcode grouping lags.
// (File inclusion is managed by project.pbxproj.)

struct DashboardView: View {
    @Binding var selectedTab: Int
    @StateObject private var viewModel: DashboardViewModel
    @EnvironmentObject private var container: AppContainer

    init(selectedTab: Binding<Int>, viewModel: DashboardViewModel) {
        self._selectedTab = selectedTab
        self._viewModel = StateObject(wrappedValue: viewModel)
    }

    private var latestHR: Double {
        viewModel.snapshot?.recentVitals.first(where: { $0.type == .heartRate })?.value ?? 0
    }
    private var latestSpO2: Double {
        viewModel.snapshot?.recentVitals.first(where: { $0.type == .spO2 })?.value ?? 0
    }
    private var latestSteps: Double {
        viewModel.snapshot?.recentVitals.first(where: { $0.type == .steps })?.value ?? 0
    }
    private var sleepMinutes: Int {
        guard let session = viewModel.snapshot?.sleepSessions.first else { return 0 }
        return session.durationMinutes
    }
    private var sleepQuality: Double {
        viewModel.snapshot?.sleepSessions.first?.qualityScore ?? 0
    }
    private var hasProfile: Bool {
        viewModel.snapshot?.patientProfile != nil
    }

    var body: some View {
        ZStack(alignment: .top) {
            ScrollView(showsIndicators: false) {
                VStack(spacing: 0) {
                    Spacer().frame(height: 80)

                    if hasProfile {
                        HomeWidgetGrid(
                            heartRate: latestHR,
                            spO2: latestSpO2,
                            steps: latestSteps,
                            envReading: viewModel.snapshot?.environmentReading,
                            sleepMinutes: sleepMinutes,
                            sleepQuality: sleepQuality,
                            coachingAdvice: viewModel.snapshot?.coachingAdvice,
                            hasProfile: hasProfile,
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
    }
}

