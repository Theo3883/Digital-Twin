import SwiftUI

struct NoPatientProfileProfileCard: View {
    let onCreateProfile: () -> Void

    var body: some View {
        VStack(spacing: 14) {
            Image(systemName: "person.crop.circle.badge.plus")
                .font(.system(size: 36))
                .foregroundColor(LiquidGlass.amberWarning)

            Text("No medical profile yet")
                .font(.subheadline.weight(.semibold))
                .foregroundColor(.white)

            Text("Create your medical profile to unlock personalized health insights and vitals tracking.")
                .font(.caption)
                .foregroundColor(.white.opacity(0.5))
                .multilineTextAlignment(.center)

            Button(action: onCreateProfile) {
                HStack(spacing: 6) {
                    Image(systemName: "plus.circle.fill")
                    Text("Create Medical Profile")
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
        }
        .glassCard(tint: LiquidGlass.amberWarning.opacity(0.08))
    }
}

