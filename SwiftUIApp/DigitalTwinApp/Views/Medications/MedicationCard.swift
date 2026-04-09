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
        default: return LiquidGlass.tealPrimary
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
                        .foregroundColor(.white)
                    if let dosage = medication.dosage {
                        Text(dosage)
                            .font(.caption)
                            .foregroundColor(.white.opacity(0.5))
                    }
                }

                if let frequency = medication.frequency {
                    Text(frequency)
                        .font(.system(size: 12))
                        .foregroundColor(.white.opacity(0.5))
                }

                if let reason = medication.reason {
                    Text(reason)
                        .font(.system(size: 11))
                        .foregroundColor(.white.opacity(0.35))
                        .lineLimit(1)
                }

                if !medication.isActive, let endDate = medication.endDate {
                    Text("Ended \(endDate.formatted(date: .abbreviated, time: .omitted))")
                        .font(.caption2)
                        .foregroundColor(.white.opacity(0.35))
                }
            }

            Spacer()

            VStack(alignment: .trailing, spacing: 6) {
                if let routeLabel = routeDisplay {
                    Text(routeLabel)
                        .font(.system(size: 10, weight: .medium))
                        .foregroundColor(LiquidGlass.tealPrimary)
                        .padding(.horizontal, 8)
                        .padding(.vertical, 3)
                        .background {
                            RoundedRectangle(cornerRadius: LiquidGlass.radiusChip)
                                .stroke(LiquidGlass.tealPrimary.opacity(0.4), lineWidth: 1)
                                .background(
                                    RoundedRectangle(cornerRadius: LiquidGlass.radiusChip)
                                        .fill(LiquidGlass.tealPrimary.opacity(0.1))
                                )
                        }
                }

                Text(addedByLabel)
                    .font(.system(size: 10, weight: .medium))
                    .foregroundColor(addedByColor)
                    .padding(.horizontal, 8)
                    .padding(.vertical, 3)
                    .background {
                        RoundedRectangle(cornerRadius: LiquidGlass.radiusChip)
                            .stroke(addedByColor.opacity(0.4), lineWidth: 1)
                            .background(
                                RoundedRectangle(cornerRadius: LiquidGlass.radiusChip)
                                    .fill(addedByColor.opacity(0.1))
                            )
                    }
            }

            Image(systemName: "chevron.right")
                .font(.caption)
                .foregroundColor(.white.opacity(0.3))
        }
        .glassCard()
    }
}

