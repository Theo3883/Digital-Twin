import SwiftUI

struct VitalStatTile: View {
    let label: String
    let value: String
    let unit: String
    let icon: String
    let color: Color

    var body: some View {
        VStack(spacing: 6) {
            Image(systemName: icon)
                .font(.system(size: 16))
                .foregroundColor(color)
            Text(value)
                .font(.system(size: 22, weight: .semibold, design: .rounded))
                .foregroundColor(.white)
            if !unit.isEmpty {
                Text(unit)
                    .font(.system(size: 10))
                    .foregroundColor(.white.opacity(0.35))
            }
            Text(label)
                .font(.system(size: 11))
                .foregroundColor(.white.opacity(0.5))
        }
        .frame(maxWidth: .infinity)
        .padding(.vertical, 16)
        .glassEffect(.regular, in: RoundedRectangle(cornerRadius: LiquidGlass.radiusInner))
    }
}

