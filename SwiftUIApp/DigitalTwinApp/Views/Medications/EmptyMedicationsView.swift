import SwiftUI

struct EmptyMedicationsView: View {
    var body: some View {
        VStack(spacing: 16) {
            Image(systemName: "pills.fill")
                .font(.system(size: 50))
                .foregroundColor(.white.opacity(0.3))
            Text("No Medications")
                .font(.title3)
                .fontWeight(.semibold)
                .foregroundColor(.white)
            Text("Add your medications to track them and check for interactions.")
                .font(.subheadline)
                .foregroundColor(.white.opacity(0.65))
                .multilineTextAlignment(.center)
        }
        .frame(maxWidth: .infinity)
        .padding(.vertical, 60)
    }
}

