import SwiftUI

struct InteractionsSheet: View {
    let interactions: [MedicationInteractionInfo]
    let medications: [MedicationInfo]
    @Environment(\.dismiss) private var dismiss

    var body: some View {
        NavigationView {
            List(interactions) { interaction in
                VStack(alignment: .leading, spacing: 8) {
                    HStack {
                        Text(interaction.displayPair(using: medications))
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
            .scrollIndicators(.hidden)
            .navigationTitle("Interactions")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .confirmationAction) { Button("Done") { dismiss() } }
            }
        }
    }
}

