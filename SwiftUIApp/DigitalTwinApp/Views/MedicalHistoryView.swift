import SwiftUI

struct MedicalHistoryView: View {
    @EnvironmentObject var engineWrapper: MobileEngineWrapper

    var body: some View {
        NavigationView {
            ScrollView(showsIndicators: false) {
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
                Text(String(format: "%.0f%%", entry.confidence * 100))
                    .glassChip(tint: entry.confidence > 0.8 ? LiquidGlass.greenPositive :
                                     entry.confidence > 0.5 ? LiquidGlass.amberWarning :
                                     LiquidGlass.redCritical)
            }

            if !entry.medicationName.isEmpty {
                HStack(spacing: 8) {
                    Label(entry.medicationName, systemImage: "pills")
                        .font(.subheadline)
                    if !entry.dosage.isEmpty {
                        Text("• \(entry.dosage)")
                            .font(.caption).foregroundColor(.secondary)
                    }
                    if !entry.frequency.isEmpty {
                        Text("• \(entry.frequency)")
                            .font(.caption).foregroundColor(.secondary)
                    }
                }
            }

            if !entry.summary.isEmpty {
                Text(entry.summary)
                    .font(.caption)
                    .foregroundColor(.secondary)
                    .lineLimit(3)
            }

            if !entry.notes.isEmpty {
                Text(entry.notes)
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
