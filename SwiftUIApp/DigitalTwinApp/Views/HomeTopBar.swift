import SwiftUI

struct HomeTopBar: View {
    let user: UserInfo?
    var onAvatarTap: () -> Void = {}

    private var greeting: String {
        let h = Calendar.current.component(.hour, from: Date())
        if h < 12 { return "Good Morning" }
        if h < 18 { return "Good Afternoon" }
        return "Good Evening"
    }

    private var userName: String {
        user?.displayName ?? "User"
    }

    private var userInitials: String {
        userName.split(separator: " ")
            .prefix(2)
            .compactMap { $0.first.map(String.init) }
            .joined()
            .uppercased()
    }

    var body: some View {
        HStack {
            HStack(spacing: 12) {
                // Avatar with glass circle
                Button { onAvatarTap() } label: {
                    if let photoUrl = user?.photoUrl, let url = URL(string: photoUrl) {
                        AsyncImage(url: url) { image in
                            image.resizable().aspectRatio(contentMode: .fill)
                        } placeholder: {
                            avatarPlaceholder
                        }
                        .frame(width: 40, height: 40)
                        .clipShape(Circle())
                        .glassEffect(.regular, in: Circle())
                    } else {
                        avatarPlaceholder
                            .glassEffect(.regular, in: Circle())
                    }
                }
                .buttonStyle(.plain)

                VStack(alignment: .leading, spacing: 2) {
                    Text(userName)
                        .font(.system(size: 18, weight: .semibold))
                        .foregroundColor(.white)
                    Text(greeting)
                        .font(.system(size: 12))
                        .foregroundColor(.white.opacity(0.65))
                }
            }

            Spacer()

            // Notification bell
            Button(action: {}) {
                ZStack(alignment: .topTrailing) {
                    Image(systemName: "bell.fill")
                        .font(.system(size: 18))
                        .foregroundColor(.white)

                    Circle()
                        .fill(LiquidGlass.redCritical)
                        .frame(width: 8, height: 8)
                        .offset(x: 2, y: -2)
                }
            }
            .glassPill()
        }
        .padding(.horizontal, 16)
        .padding(.vertical, 12)
        .glassEffect(.regular, in: RoundedRectangle(cornerRadius: LiquidGlass.radiusCard))
        .padding(.horizontal, 8)
        .padding(.top, 4)
    }

    private var avatarPlaceholder: some View {
        ZStack {
            Circle()
                .fill(
                    LinearGradient(
                        colors: [
                            Color(red: 96/255, green: 165/255, blue: 250/255),
                            Color(red: 168/255, green: 85/255, blue: 247/255)
                        ],
                        startPoint: .topLeading,
                        endPoint: .bottomTrailing
                    )
                )
                .frame(width: 40, height: 40)
            Text(userInitials)
                .font(.system(size: 14, weight: .bold))
                .foregroundColor(.white)
        }
    }
}

