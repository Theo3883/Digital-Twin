import SwiftUI

struct NoPatientProfileHomeView: View {
    let onCreateProfile: () -> Void

    var body: some View {
        VStack(spacing: 16) {
            ZStack {
                Circle()
                    .fill(LiquidGlass.amberWarning.opacity(0.15))
                    .frame(width: 72, height: 72)
                Image(systemName: "person.crop.circle.badge.plus")
                    .font(.system(size: 34))
                    .foregroundColor(LiquidGlass.amberWarning)
            }

            Text("Create your patient profile")
                .font(.system(size: 20, weight: .bold))
                .foregroundColor(.white)

            Text("Set up your medical profile to unlock personalized insights, vitals tracking, and ECG monitoring.")
                .font(.subheadline)
                .foregroundColor(.white.opacity(0.65))
                .multilineTextAlignment(.center)

            Button(action: onCreateProfile) {
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
        }
        .padding(16)
        .glassCard(tint: LiquidGlass.amberWarning.opacity(0.08))
        .padding(.horizontal, 16)
    }
}

