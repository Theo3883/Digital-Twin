import SwiftUI

struct NotificationsListView: View {
    @EnvironmentObject private var engineWrapper: MobileEngineWrapper
    @StateObject private var viewModel = NotificationsViewModel()

    var body: some View {
        NavigationView {
            Group {
                if viewModel.isLoading {
                    ProgressView()
                        .progressViewStyle(.circular)
                } else if !engineWrapper.isCloudAuthenticated {
                    cloudDisabledState
                } else if viewModel.items.isEmpty {
                    emptyState
                } else {
                    list
                }
            }
            .navigationTitle("Notifications")
            .liquidGlassNavigationStyle()
            .toolbar {
                ToolbarItem(placement: .topBarTrailing) {
                    if viewModel.unreadCount > 0 {
                        Button("Mark All Read") {
                            Task { await viewModel.markAllRead() }
                        }
                    }
                }
            }
            .task(id: engineWrapper.isCloudAuthenticated) {
                await viewModel.refresh(using: engineWrapper)
            }
            .refreshable {
                await viewModel.refresh(using: engineWrapper)
            }
        }
    }

    private var list: some View {
        List {
            ForEach(viewModel.items) { item in
                NotificationRow(item: item)
                    .listRowBackground(Color.clear)
                    .contextMenu {
                        if item.isUnread {
                            Button("Mark Read") {
                                Task { await viewModel.markRead(item) }
                            }
                        }
                        Button("Delete", role: .destructive) {
                            Task { await viewModel.delete(item) }
                        }
                    }
            }
        }
        .listStyle(.plain)
        .scrollContentBackground(.hidden)
    }

    private var emptyState: some View {
        VStack(spacing: 16) {
            Image(systemName: "bell.slash")
                .font(.system(size: 44))
                .foregroundColor(.white.opacity(0.6))
            Text("No notifications yet")
                .font(.title3.weight(.semibold))
                .foregroundColor(.white)
            Text("We will surface updates from your care team here.")
                .font(.subheadline)
                .foregroundColor(.white.opacity(0.65))
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .padding(.vertical, 60)
    }

    private var cloudDisabledState: some View {
        VStack(spacing: 16) {
            Image(systemName: "icloud.slash")
                .font(.system(size: 44))
                .foregroundColor(.white.opacity(0.6))
            Text("Cloud notifications unavailable")
                .font(.title3.weight(.semibold))
                .foregroundColor(.white)
            Text("Finish cloud sign-in / sync to fetch notifications.")
                .font(.subheadline)
                .foregroundColor(.white.opacity(0.65))
                .multilineTextAlignment(.center)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .padding(.vertical, 60)
    }
}

private struct NotificationRow: View {
    let item: NotificationInfo

    var body: some View {
        HStack(alignment: .top, spacing: 12) {
            Circle()
                .fill(item.isUnread ? LiquidGlass.redCritical : Color.white.opacity(0.2))
                .frame(width: 8, height: 8)
                .padding(.top, 6)

            VStack(alignment: .leading, spacing: 6) {
                HStack(alignment: .top) {
                    Text(item.title)
                        .font(.headline)
                        .foregroundColor(.white)
                    Spacer()
                    Text(item.createdAt, style: .date)
                        .font(.caption)
                        .foregroundColor(.white.opacity(0.6))
                }

                Text(item.body)
                    .font(.subheadline)
                    .foregroundColor(.white.opacity(0.75))
            }
        }
        .padding(12)
        .glassEffect(.regular, in: RoundedRectangle(cornerRadius: LiquidGlass.radiusCard))
        .listRowInsets(EdgeInsets())
        .padding(.vertical, 6)
    }
}

@MainActor
final class NotificationsViewModel: ObservableObject {
    @Published var items: [NotificationInfo] = []
    @Published var unreadCount: Int = 0
    @Published var isLoading = false

    private let service = NotificationService()
    private var engine: MobileEngineClient?
    private var lastRefreshAt: Date?

    func refresh(using wrapper: MobileEngineWrapper) async {
        if isLoading { return }
        if let lastRefreshAt, Date().timeIntervalSince(lastRefreshAt) < 1.0 { return }
        lastRefreshAt = Date()

        self.engine = wrapper.mobileEngineClient

        isLoading = true
        defer { isLoading = false }

        guard let client = wrapper.mobileEngineClient else {
            print("[CloudDebug][Notifications] engine client nil")
            items = []
            unreadCount = 0
            return
        }

        do {
            print("[CloudDebug][Notifications] refresh start isCloudAuthenticated=\(wrapper.isCloudAuthenticated)")
            let list = try await service.fetchNotifications(engine: client, limit: 100, unreadOnly: false)
            let count = try await service.fetchUnreadCount(engine: client)
            items = list
            unreadCount = count
            print("[CloudDebug][Notifications] refresh ok items=\(list.count) unread=\(count)")
        } catch {
            print("[CloudDebug][Notifications] refresh failed \(error.localizedDescription)")
            items = []
            unreadCount = 0
        }
    }

    func markRead(_ item: NotificationInfo) async {
        // TODO: Implement mark read via engine when API layer support is added
        // For now, update local state only
        updateLocalRead(id: item.id)
    }

    func markAllRead() async {
        // TODO: Implement mark all read via engine when API layer support is added
        // For now, update local state only
        items = items.map { NotificationInfo(id: $0.id, title: $0.title, body: $0.body, type: $0.type, severity: $0.severity, patientId: $0.patientId, actorUserId: $0.actorUserId, actorName: $0.actorName, createdAt: $0.createdAt, readAt: Date()) }
        unreadCount = 0
    }

    func delete(_ item: NotificationInfo) async {
        // TODO: Implement delete via engine when API layer support is added
        // For now, update local state only
        items.removeAll { $0.id == item.id }
        unreadCount = items.filter { $0.isUnread }.count
    }

    private func updateLocalRead(id: UUID) {
        items = items.map { item in
            guard item.id == id else { return item }
            return NotificationInfo(id: item.id, title: item.title, body: item.body, type: item.type, severity: item.severity, patientId: item.patientId, actorUserId: item.actorUserId, actorName: item.actorName, createdAt: item.createdAt, readAt: Date())
        }
        unreadCount = items.filter { $0.isUnread }.count
    }
}
