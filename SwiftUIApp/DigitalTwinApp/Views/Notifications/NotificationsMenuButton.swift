import SwiftUI

struct NotificationsMenuButton: View {
    @EnvironmentObject private var engineWrapper: MobileEngineWrapper
    @StateObject private var viewModel = NotificationsBellViewModel()
    @State private var isPresented = false

    var body: some View {
        Button {
            isPresented = true
        } label: {
            ZStack(alignment: .topTrailing) {
                Image(systemName: "bell.fill")
                    .font(.system(size: 18))
                    .foregroundColor(.white)

                if viewModel.unreadCount > 0 {
                    Text(viewModel.unreadCount > 99 ? "99+" : "\(viewModel.unreadCount)")
                        .font(.system(size: 10, weight: .bold))
                        .foregroundColor(.white)
                        .padding(4)
                        .background(Capsule().fill(LiquidGlass.redCritical))
                        .offset(x: 6, y: -6)
                }
            }
        }
        .glassPill()
        .sheet(isPresented: $isPresented) {
            NotificationsListView()
                .environmentObject(engineWrapper)
        }
        .task(id: engineWrapper.isCloudAuthenticated) {
            await viewModel.refresh(using: engineWrapper)
        }
    }
}

@MainActor
final class NotificationsBellViewModel: ObservableObject {
    @Published var unreadCount: Int = 0
    private let service = NotificationService.shared
    private var lastRefreshAt: Date?

    func refresh(using wrapper: MobileEngineWrapper) async {
        if let lastRefreshAt, Date().timeIntervalSince(lastRefreshAt) < 2.0 { return }
        lastRefreshAt = Date()

        guard wrapper.isCloudAuthenticated else {
            unreadCount = 0
            return
        }
        guard let client = wrapper.mobileEngineClient else {
            unreadCount = 0
            return
        }
        do {
            let list = try await service.fetchNotifications(engine: client, limit: 1000, unreadOnly: false)
            unreadCount = list.filter { $0.readAt == nil }.count
        } catch {
            unreadCount = 0
        }
    }
}
