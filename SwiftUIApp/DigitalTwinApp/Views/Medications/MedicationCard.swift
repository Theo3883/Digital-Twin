import SwiftUI

struct MedicationCard: View {
    let medication: MedicationInfo

    private var addedByLabel: String {
        switch medication.addedByRole {
        case 1: return "Prescribed"
        case 2: return "OCR Scan"
        default: return "Self-added"
        }
    }

    private var addedByColor: Color {
        switch medication.addedByRole {
        case 1: return Color(red: 168/255, green: 85/255, blue: 247/255)
        case 2: return Color(red: 245/255, green: 158/255, blue: 11/255)
        default: return .teal
        }
    }

    private var routeDisplay: String? {
        switch medication.route {
        case 0: return "Oral"
        case 1: return "IV"
        case 2: return "IM"
        case 3: return "Topical"
        case 4: return "Inhaled"
        case 5: return "Sublingual"
        default: return nil
        }
    }

    var body: some View {
        HStack(spacing: 12) {
            VStack(alignment: .leading, spacing: 4) {
                HStack(spacing: 6) {
                    Text(medication.name)
                        .font(.system(size: 15, weight: .semibold))
                        .foregroundStyle(.primary)
                    Text(medication.dosage)
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }

                if let frequency = medication.frequency {
                    Text(frequency)
                        .font(.system(size: 12))
                        .foregroundStyle(.secondary)
                }

                if let reason = medication.reason {
                    Text(reason)
                        .font(.system(size: 11))
                        .foregroundStyle(.tertiary)
                        .lineLimit(1)
                }

                if !medication.isActive, let endDate = medication.endDate {
                    Text("Ended \(endDate.formatted(date: .abbreviated, time: .omitted))")
                        .font(.caption2)
                        .foregroundStyle(.tertiary)
                }
            }

            Spacer()

            VStack(alignment: .trailing, spacing: 6) {
                if let routeLabel = routeDisplay {
                    Text(routeLabel)
                        .font(.system(size: 10, weight: .medium))
                        .foregroundStyle(.teal)
                        .padding(.horizontal, 8)
                        .padding(.vertical, 3)
                        .background {
                            Capsule(style: .continuous)
                                .fill(.teal.opacity(0.14))
                        }
                }

                Text(addedByLabel)
                    .font(.system(size: 10, weight: .medium))
                    .foregroundColor(addedByColor)
                    .padding(.horizontal, 8)
                    .padding(.vertical, 3)
                    .background {
                        Capsule(style: .continuous)
                            .fill(addedByColor.opacity(0.14))
                    }
            }

            Image(systemName: "chevron.right")
                .font(.caption)
                .foregroundStyle(.tertiary)
        }
        .padding()
        .background(.ultraThinMaterial, in: RoundedRectangle(cornerRadius: 24, style: .continuous))
        .overlay {
            RoundedRectangle(cornerRadius: 24, style: .continuous)
                .strokeBorder(.white.opacity(0.15), lineWidth: 1)
        }
    }
}

