import SwiftUI

struct MedicationSafetyBadge: View {
    let severity: Int

    private var label: String {
        switch severity {
        case 3: return "High"
        case 2: return "Medium"
        case 1: return "Low"
        default: return "None"
        }
    }

    private var color: Color {
        switch severity {
        case 3: return .red
        case 2: return .orange
        case 1: return .green
        default: return .gray
        }
    }

    var body: some View {
        Text(label)
            .font(.system(size: 10, weight: .bold))
            .foregroundStyle(color)
            .padding(.horizontal, 8)
            .padding(.vertical, 3)
            .background {
                Capsule(style: .continuous)
                    .fill(color.opacity(0.16))
            }
    }
}

