import SwiftUI

struct ProfileSetupGateView: View {
    @EnvironmentObject private var container: AppContainer

    var body: some View {
        ScrollView {
            VStack(spacing: 24) {
                Spacer(minLength: 60)

                ZStack {
                    Circle()
                        .fill(LiquidGlass.tealPrimary.opacity(0.15))
                        .frame(width: 88, height: 88)
                    Image(systemName: "person.text.rectangle")
                        .font(.system(size: 36, weight: .semibold))
                        .foregroundColor(LiquidGlass.tealPrimary)
                }

                VStack(spacing: 10) {
                    Text("Create your patient profile")
                        .font(.title2.weight(.bold))
                        .foregroundColor(.white)
                    Text("Before continuing, we need a few medical details to personalize insights, vitals, and recommendations.")
                        .font(.subheadline)
                        .foregroundColor(.white.opacity(0.65))
                        .multilineTextAlignment(.center)
                        .padding(.horizontal, 24)
                }

                Button {
                    container.shouldPresentProfileEdit = true
                } label: {
                    HStack(spacing: 8) {
                        Image(systemName: "plus.circle.fill")
                        Text("Create patient profile")
                            .font(.subheadline.weight(.semibold))
                    }
                    .foregroundColor(.white)
                    .frame(maxWidth: .infinity)
                    .padding(.vertical, 12)
                    .background {
                        RoundedRectangle(cornerRadius: LiquidGlass.radiusCard)
                            .fill(LiquidGlass.tealPrimary.opacity(0.3))
                    }
                }
                .padding(.horizontal, 16)

                Spacer(minLength: 40)
            }
            .padding(.vertical, 20)
        }
        .pageEnterAnimation()
    }
}

