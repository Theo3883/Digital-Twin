import SwiftUI

struct DashboardView: View {
    @Binding var selectedTab: Int
    @StateObject private var viewModel: DashboardViewModel

    init(selectedTab: Binding<Int>, viewModel: DashboardViewModel) {
        self._selectedTab = selectedTab
        self._viewModel = StateObject(wrappedValue: viewModel)
    }

    private var latestHR: Double {
        viewModel.snapshot?.recentVitals.first(where: { $0.type == .heartRate })?.value ?? 0
    }
    private var latestSpO2: Double {
        viewModel.snapshot?.recentVitals.first(where: { $0.type == .oxygenSaturation })?.value ?? 0
    }
    private var latestSteps: Double {
        viewModel.snapshot?.recentVitals.first(where: { $0.type == .stepCount })?.value ?? 0
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
            ScrollView {
                VStack(spacing: 0) {
                    Spacer().frame(height: 80)

                    HomeWidgetGrid(
                        heartRate: latestHR,
                        spO2: latestSpO2,
                        steps: latestSteps,
                        envReading: viewModel.snapshot?.environmentReading,
                        sleepMinutes: sleepMinutes,
                        sleepQuality: sleepQuality,
                        insightText: viewModel.snapshot?.coachingAdvice?.advice,
                        hasProfile: hasProfile,
                        selectedTab: $selectedTab
                    )
                    .padding(.horizontal, 16)
                    .padding(.bottom, 32)
                }
            }

            HomeTopBar(user: viewModel.snapshot?.user) {
                selectedTab = 5
            }
        }
        .pageEnterAnimation()
        .task {
            await viewModel.load()
        }
        .refreshable {
            await viewModel.load()
        }
    }
}

