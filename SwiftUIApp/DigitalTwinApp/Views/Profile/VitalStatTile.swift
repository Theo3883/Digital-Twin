import SwiftUI

struct VitalStatTile: View {
    let label: String
    let value: String
    let unit: String
    let icon: String
    let color: Color

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            HStack(alignment: .top, spacing: 8) {
                Text(label)
                    .font(.system(size: 14, weight: .medium))
                    .foregroundColor(.white.opacity(0.65))
                    .lineLimit(1)

                Spacer(minLength: 4)

                Image(systemName: icon)
                    .symbolRenderingMode(.hierarchical)
                    .font(.system(size: 17, weight: .semibold))
                    .foregroundStyle(color)
                    .frame(width: 18, alignment: .trailing)
            }

            Spacer(minLength: 4)

            HStack(alignment: .center, spacing: 8) {
                Text(value)
                    .font(.system(size: 26, weight: .semibold, design: .rounded))
                    .foregroundColor(.white)

                Spacer(minLength: 8)

                if !unit.isEmpty {
                    Text(unit)
                        .font(.system(size: 14, weight: .semibold, design: .rounded))
                        .foregroundColor(.white.opacity(0.6))
                        .lineLimit(1)
                        .minimumScaleFactor(0.75)
                        .frame(alignment: .trailing)
                }
            }

            Spacer(minLength: 4)
        }
        .frame(maxWidth: .infinity)
        .frame(minHeight: 86, alignment: .topLeading)
        .padding(.horizontal, 10)
        .padding(.vertical, 8)
        .glassEffect(.regular, in: RoundedRectangle(cornerRadius: LiquidGlass.radiusInner))
    }
}

