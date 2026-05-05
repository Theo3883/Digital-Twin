import SwiftUI

struct MetricTile: View {
    let label: String
    let topSymbol: String
    let symbolColor: Color
    let value: String
    let unit: String
    let status: String
    let statusColor: Color

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            HStack(alignment: .top, spacing: 8) {
                Text(label)
                    .font(.system(size: 14, weight: .medium))
                    .foregroundColor(.white.opacity(0.65))
                    .lineLimit(1)

                Spacer(minLength: 4)

                Image(systemName: topSymbol)
                    .symbolRenderingMode(.hierarchical)
                    .font(.system(size: 17, weight: .semibold))
                    .foregroundStyle(symbolColor)
                    .frame(width: 18, alignment: .trailing)
            }

            Spacer(minLength: 4)

            HStack(alignment: .center, spacing: 8) {
                HStack(alignment: .firstTextBaseline, spacing: 5) {
                    Text(value)
                        .font(.system(size: 26, weight: .semibold, design: .rounded))
                        .foregroundColor(.white)
                    Text(unit)
                        .font(.system(size: 14, weight: .medium))
                        .foregroundColor(.white.opacity(0.45))
                }

                Spacer(minLength: 8)

                Text(status)
                    .font(.system(size: 14, weight: .semibold, design: .rounded))
                    .foregroundColor(statusColor)
                    .lineLimit(1)
                    .minimumScaleFactor(0.75)
                    .frame(alignment: .trailing)
            }

            Spacer(minLength: 4)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .frame(minHeight: 86, alignment: .topLeading)
        .padding(.horizontal, 10)
        .padding(.vertical, 8)
        .glassEffect(.regular, in: RoundedRectangle(cornerRadius: LiquidGlass.radiusInner))
    }
}

