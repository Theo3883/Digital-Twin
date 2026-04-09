import SwiftUI

struct MetricTile: View {
    let icon: String
    let iconColor: Color
    let label: String
    let value: String
    let unit: String
    let status: String
    let statusColor: Color

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            HStack(spacing: 6) {
                Image(systemName: icon)
                    .font(.system(size: 14))
                    .foregroundColor(iconColor)
                Text(label)
                    .font(.caption)
                    .foregroundColor(.white.opacity(0.65))
            }

            Spacer()

            HStack(alignment: .firstTextBaseline, spacing: 4) {
                Text(value)
                    .font(.system(size: 20, weight: .semibold, design: .rounded))
                    .foregroundColor(.white)
                Text(unit)
                    .font(.system(size: 11))
                    .foregroundColor(.white.opacity(0.4))
            }

            Spacer()

            Text(status)
                .font(.system(size: 11, weight: .medium))
                .foregroundColor(statusColor)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .frame(height: 100)
        .padding(12)
        .glassEffect(.regular, in: RoundedRectangle(cornerRadius: LiquidGlass.radiusInner))
    }
}

