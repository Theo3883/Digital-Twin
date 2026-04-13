import SwiftUI

struct MedicationsView: View {
    @StateObject private var viewModel: MedicationsViewModel
    private let addSheetViewModel: AddMedicationSheetViewModel

    init(viewModel: MedicationsViewModel, addSheetViewModel: AddMedicationSheetViewModel) {
        _viewModel = StateObject(wrappedValue: viewModel)
        self.addSheetViewModel = addSheetViewModel
    }

    var body: some View {
        ZStack(alignment: .bottom) {
            ScrollView {
                VStack(spacing: 16) {
                    // Interaction Banner
                    interactionBanner
                    
                    // Interaction Detail Cards
                    if !viewModel.interactions.isEmpty {
                        interactionDetailCards
                    }
                    
                    // Active Medications
                    if !viewModel.activeMedications.isEmpty {
                        activeMedicationsSection
                    }
                    
                    // Ended Medications
                    if !viewModel.inactiveMedications.isEmpty {
                        endedMedicationsSection
                    }
                    
                    if viewModel.medications.isEmpty {
                        EmptyMedicationsView()
                    }
                    
                    Spacer(minLength: 80)
                }
                .padding(16)
            }
            .pageEnterAnimation()
            .task {
                await viewModel.refresh()
            }
            .refreshable {
                await viewModel.refresh()
            }
            
            // Floating Action Buttons
            floatingActionButtons
        }
        .sheet(isPresented: $viewModel.isAddSheetPresented) {
            AddMedicationSheet(viewModel: addSheetViewModel)
        }
        .sheet(isPresented: $viewModel.isInteractionsSheetPresented) {
            InteractionsSheet(interactions: viewModel.interactions, medications: viewModel.medications)
        }
        .alert("End Medication", isPresented: $viewModel.isEndReasonDialogPresented) {
            TextField("Reason (optional)", text: $viewModel.endReason)
            Button("End") {
                viewModel.confirmEndMedication()
            }
            Button("Cancel", role: .cancel) { viewModel.endReason = "" }
        }
    }

    // MARK: - Interaction Banner

    private var interactionBanner: some View {
        Group {
            if viewModel.interactions.isEmpty && !viewModel.activeMedications.isEmpty {
                HStack(spacing: 8) {
                    Image(systemName: "checkmark.shield.fill")
                        .foregroundColor(LiquidGlass.greenPositive)
                    Text("✓ No Interactions Found")
                        .font(.subheadline.weight(.medium))
                        .foregroundColor(.white)
                }
                .frame(maxWidth: .infinity)
                .glassBanner(tint: LiquidGlass.greenPositive.opacity(0.15))
            } else if !viewModel.interactions.isEmpty {
                let highCount = viewModel.interactions.filter { $0.severity == 3 }.count
                let medCount = viewModel.interactions.filter { $0.severity == 2 }.count
                let lowCount = viewModel.interactions.filter { $0.severity == 1 }.count
                
                VStack(alignment: .leading, spacing: 6) {
                    HStack(spacing: 8) {
                        Image(systemName: "exclamationmark.triangle.fill")
                            .foregroundColor(.white)
                        Text("⚠ \(viewModel.interactions.count) Interaction\(viewModel.interactions.count == 1 ? "" : "s") Detected")
                            .font(.subheadline.weight(.semibold))
                            .foregroundColor(.white)
                    }
                    HStack(spacing: 8) {
                        if highCount > 0 {
                            Text("\(highCount) High")
                                .font(.caption2.weight(.medium))
                                .glassChip(tint: LiquidGlass.redCritical)
                        }
                        if medCount > 0 {
                            Text("\(medCount) Medium")
                                .font(.caption2.weight(.medium))
                                .glassChip(tint: LiquidGlass.amberWarning)
                        }
                        if lowCount > 0 {
                            Text("\(lowCount) Low")
                                .font(.caption2.weight(.medium))
                                .glassChip(tint: LiquidGlass.greenPositive)
                        }
                    }
                }
                .frame(maxWidth: .infinity, alignment: .leading)
                .glassBanner(tint: highCount > 0 ? LiquidGlass.redCritical.opacity(0.3) : LiquidGlass.amberWarning.opacity(0.3))
            } else if viewModel.medications.isEmpty {
                HStack(spacing: 8) {
                    Image(systemName: "pills.fill")
                        .foregroundColor(.white.opacity(0.5))
                    Text("No Medications")
                        .font(.subheadline)
                        .foregroundColor(.white.opacity(0.65))
                }
                .frame(maxWidth: .infinity)
                .glassBanner(tint: LiquidGlass.greenPositive.opacity(0.1))
            }
        }
    }

    // MARK: - Interaction Detail Cards

    private var interactionDetailCards: some View {
        VStack(spacing: 8) {
            ForEach(viewModel.interactions) { interaction in
                HStack(alignment: .top, spacing: 12) {
                    Image(systemName: "shield.fill")
                        .foregroundColor(interaction.severity == 3 ? LiquidGlass.redCritical : (interaction.severity == 2 ? LiquidGlass.amberWarning : LiquidGlass.greenPositive))
                        .font(.title3)
                    
                    VStack(alignment: .leading, spacing: 6) {
                        HStack {
                            Text(interaction.displayPair(using: viewModel.medications))
                                .font(.subheadline.weight(.medium))
                                .foregroundColor(.white)
                            Spacer()
                            MedicationSafetyBadge(severity: interaction.severity)
                        }
                        Text(interaction.description)
                            .font(.caption)
                            .foregroundColor(.white.opacity(0.65))
                            .lineLimit(3)
                    }
                }
                .padding()
                .overlay(alignment: .leading) {
                    Rectangle()
                        .fill(interaction.severity == 3 ? LiquidGlass.redCritical : (interaction.severity == 2 ? LiquidGlass.amberWarning : LiquidGlass.greenPositive))
                        .frame(width: 4)
                }
                .glassEffect(.regular, in: RoundedRectangle(cornerRadius: LiquidGlass.radiusCard))
            }
        }
    }

    // MARK: - Active Medications Section

    private var activeMedicationsSection: some View {
        VStack(alignment: .leading, spacing: 12) {
            Text("Active Medications · \(viewModel.activeMedications.count) drug\(viewModel.activeMedications.count == 1 ? "" : "s")")
                .font(.subheadline.weight(.medium))
                .foregroundColor(.white.opacity(0.65))
                .padding(.horizontal, 4)
            
            ForEach(viewModel.activeMedications) { med in
                MedicationCard(medication: med)
                    .contextMenu {
                        Button {
                            viewModel.promptEndMedication(med)
                        } label: {
                            Label("End Medication", systemImage: "stop.circle")
                        }
                        Button(role: .destructive) {
                            viewModel.removeMedication(med)
                        } label: {
                            Label("Remove", systemImage: "trash")
                        }
                    }
            }
        }
    }

    // MARK: - Ended Medications Section

    private var endedMedicationsSection: some View {
        VStack(alignment: .leading, spacing: 12) {
            Text("Ended · \(viewModel.inactiveMedications.count) medication\(viewModel.inactiveMedications.count == 1 ? "" : "s")")
                .font(.caption.weight(.medium))
                .foregroundColor(.white.opacity(0.4))
                .padding(.horizontal, 4)
            
            ForEach(viewModel.inactiveMedications) { med in
                MedicationCard(medication: med)
                    .opacity(0.75)
            }
        }
    }

    // MARK: - FABs

    private var floatingActionButtons: some View {
        HStack(spacing: 12) {
            Button(action: { viewModel.isInteractionsSheetPresented = true }) {
                HStack(spacing: 6) {
                    Image(systemName: "magnifyingglass")
                        .font(.system(size: 14))
                    Text("Check Interactions")
                        .font(.caption.weight(.medium))
                }
                .foregroundColor(.white)
            }
            .glassPill(tint: LiquidGlass.tealPrimary.opacity(0.15))
            .disabled(viewModel.interactions.isEmpty)
            
            Button(action: { viewModel.isAddSheetPresented = true }) {
                HStack(spacing: 6) {
                    Image(systemName: "plus")
                        .font(.system(size: 14))
                    Text("Add Medication")
                        .font(.caption.weight(.medium))
                }
                .foregroundColor(.white)
            }
            .glassPill(tint: LiquidGlass.tealPrimary.opacity(0.2))
        }
        .padding(.bottom, 16)
    }
}
