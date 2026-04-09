import SwiftUI

struct MedicationSafetyBadge: View {
    let severity: Int

    private var label: String {
        switch severity {
        case 2: return "High"
        case 1: return "Medium"
        default: return "Low"
        }
    }

    private var color: Color {
        switch severity {
        case 2: return LiquidGlass.redCritical
        case 1: return LiquidGlass.amberWarning
        default: return LiquidGlass.greenPositive
        }
    }

    var body: some View {
        Text(label)
            .font(.system(size: 10, weight: .bold))
            .foregroundColor(color)
            .padding(.horizontal, 8)
            .padding(.vertical, 3)
            .background {
                RoundedRectangle(cornerRadius: LiquidGlass.radiusChip)
                    .fill(color.opacity(0.15))
            }
    }
}

