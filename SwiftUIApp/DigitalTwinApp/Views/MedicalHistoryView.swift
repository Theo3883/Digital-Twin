import SwiftUI

struct MedicalHistoryView: View {
    @EnvironmentObject var engineWrapper: MobileEngineWrapper

    var body: some View {
        NavigationView {
            ScrollView {
                VStack(spacing: 16) {
                    if engineWrapper.medicalHistory.isEmpty {
                        EmptyHistoryView()
                    } else {
                        ForEach(engineWrapper.medicalHistory) { entry in
                            MedicalHistoryCard(entry: entry)
                        }
                    }

                    Spacer(minLength: 100)
                }
                .padding()
            }
            .navigationTitle("Medical History")
            .liquidGlassNavigationStyle()
            .task {
                await engineWrapper.loadMedicalHistory()
            }
            .refreshable {
                await engineWrapper.loadMedicalHistory()
            }
        }
    }
}

// MARK: - History Card

struct MedicalHistoryCard: View {
    let entry: MedicalHistoryEntryInfo

    var body: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack {
                Text(entry.displayTitle)
                    .font(.headline)
                Spacer()
                if let confidence = entry.confidence {
                    Text(String(format: "%.0f%%", confidence * 100))
                        .glassChip(tint: confidence > 0.8 ? LiquidGlass.greenPositive :
                                         confidence > 0.5 ? LiquidGlass.amberWarning :
                                         LiquidGlass.redCritical)
                }
            }

            if let medicationName = entry.medicationName {
                HStack(spacing: 8) {
                    Label(medicationName, systemImage: "pills")
                        .font(.subheadline)
                    if let dosage = entry.dosage {
                        Text("• \(dosage)")
                            .font(.caption).foregroundColor(.secondary)
                    }
                    if let frequency = entry.frequency {
                        Text("• \(frequency)")
                            .font(.caption).foregroundColor(.secondary)
                    }
                }
            }

            if let summary = entry.summary {
                Text(summary)
                    .font(.caption)
                    .foregroundColor(.secondary)
                    .lineLimit(3)
            }

            if let notes = entry.notes, !notes.isEmpty {
                Text(notes)
                    .font(.caption2)
                    .foregroundColor(.secondary)
                    .lineLimit(2)
            }

            if entry.sourceDocumentId != nil {
                Label("From scanned document", systemImage: "doc.text.viewfinder")
                    .font(.caption2)
                    .foregroundColor(.blue)
            }
        }
        .glassCard()
    }
}

// MARK: - Empty State

struct EmptyHistoryView: View {
    var body: some View {
        VStack(spacing: 16) {
            Image(systemName: "doc.text.magnifyingglass")
                .font(.system(size: 50)).foregroundColor(.secondary)
            Text("No Medical History")
                .font(.title3).fontWeight(.semibold)
            Text("Scan medical documents to automatically extract and build your medical history.")
                .font(.subheadline).foregroundColor(.secondary).multilineTextAlignment(.center)
        }
        .frame(maxWidth: .infinity)
        .padding(.vertical, 60)
    }
}
