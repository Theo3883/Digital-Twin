import SwiftUI

struct InteractionsSheet: View {
    let interactions: [MedicationInteractionInfo]
    @Environment(\.dismiss) private var dismiss

    var body: some View {
        NavigationView {
            List(interactions) { interaction in
                VStack(alignment: .leading, spacing: 8) {
                    HStack {
                        Text("\(interaction.drugA) + \(interaction.drugB)")
                            .font(.headline)
                        Spacer()
                        MedicationSafetyBadge(severity: interaction.severity)
                    }
                    Text(interaction.description)
                        .font(.subheadline)
                        .foregroundColor(.secondary)
                }
                .padding(.vertical, 4)
            }
            .navigationTitle("Interactions")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .confirmationAction) { Button("Done") { dismiss() } }
            }
        }
    }
}

