import SwiftUI

struct VitalSignsView: View {
    @EnvironmentObject var engineWrapper: MobileEngineWrapper
    @State private var vitalSigns: [VitalSignInfo] = []
    @State private var selectedType: VitalSignType? = nil
    @State private var showingAddVital = false
    @State private var isLoading = false
    @State private var selectedDateRange = DateRange.week
    
    var body: some View {
        NavigationView {
            VStack(spacing: 0) {
                // Date Range Picker
                DateRangePickerView(selectedRange: $selectedDateRange)
                    .onChange(of: selectedDateRange) { _, _ in
                        Task { await loadVitalSigns() }
                    }
                
                // Vital Type Filter
                VitalTypeFilterView(selectedType: $selectedType)
                    .onChange(of: selectedType) { _, _ in
                        Task { await loadVitalSigns() }
                    }
                
                // Vital Signs List
                if isLoading {
                    ProgressView("Loading vital signs...")
                        .frame(maxWidth: .infinity, maxHeight: .infinity)
                } else if vitalSigns.isEmpty {
                    EmptyVitalSignsView()
                } else {
                    VitalSignsListView(vitalSigns: vitalSigns)
                }
            }
            .navigationTitle("Vital Signs")
            .liquidGlassNavigationStyle()
            .toolbar {
                ToolbarItem(placement: .navigationBarTrailing) {
                    Button(action: { showingAddVital = true }) {
                        Image(systemName: "plus")
                    }
                    .liquidGlassButtonStyle()
                }
            }
            .sheet(isPresented: $showingAddVital) {
                AddVitalSignView()
            }
            .task {
                await loadVitalSigns()
            }
            .refreshable {
                await loadVitalSigns()
            }
        }
    }
    
    private func loadVitalSigns() async {
        isLoading = true
        
        let (fromDate, toDate) = selectedDateRange.dateRange
        
        if let selectedType = selectedType {
            vitalSigns = await engineWrapper.getVitalSignsByType(selectedType, from: fromDate, to: toDate)
        } else {
            vitalSigns = await engineWrapper.getVitalSigns(from: fromDate, to: toDate)
        }
        
        isLoading = false
    }
}

// MARK: - Date Range Picker

struct DateRangePickerView: View {
    @Binding var selectedRange: DateRange
    
    var body: some View {
        ScrollView(.horizontal, showsIndicators: false) {
            HStack(spacing: 12) {
                ForEach(DateRange.allCases, id: \.self) { range in
                    Button(range.displayName) {
                        selectedRange = range
                    }
                    .buttonStyle(.bordered)
                    .foregroundColor(selectedRange == range ? .white : .primary)
                    .background(selectedRange == range ? Color.blue : Color.clear)
                    .liquidGlassButtonStyle()
                }
            }
            .padding(.horizontal)
        }
        .padding(.vertical, 8)
    }
}

// MARK: - Vital Type Filter

struct VitalTypeFilterView: View {
    @Binding var selectedType: VitalSignType?
    
    var body: some View {
        ScrollView(.horizontal, showsIndicators: false) {
            HStack(spacing: 12) {
                // All Types
                Button("All") {
                    selectedType = nil
                }
                .buttonStyle(.bordered)
                .foregroundColor(selectedType == nil ? .white : .primary)
                .background(selectedType == nil ? Color.blue : Color.clear)
                .liquidGlassButtonStyle()
                
                // Individual Types
                ForEach(VitalSignType.allCases, id: \.self) { type in
                    Button(type.displayName) {
                        selectedType = type
                    }
                    .buttonStyle(.bordered)
                    .foregroundColor(selectedType == type ? .white : .primary)
                    .background(selectedType == type ? Color.blue : Color.clear)
                    .liquidGlassButtonStyle()
                }
            }
            .padding(.horizontal)
        }
        .padding(.vertical, 8)
    }
}

// MARK: - Vital Signs List

struct VitalSignsListView: View {
    let vitalSigns: [VitalSignInfo]
    
    var body: some View {
        List {
            ForEach(groupedVitalSigns, id: \.date) { group in
                Section(group.date.formatted(date: .abbreviated, time: .omitted)) {
                    ForEach(group.vitals) { vital in
                        VitalSignRowView(vital: vital)
                    }
                }
            }
        }
        .scrollIndicators(.hidden)
        .listStyle(.insetGrouped)
    }
    
    private var groupedVitalSigns: [VitalSignGroup] {
        let calendar = Calendar.current
        let grouped = Dictionary(grouping: vitalSigns) { vital in
            calendar.startOfDay(for: vital.timestamp)
        }
        
        return grouped.map { date, vitals in
            VitalSignGroup(date: date, vitals: vitals.sorted { $0.timestamp > $1.timestamp })
        }.sorted { $0.date > $1.date }
    }
}

// MARK: - Vital Sign Row

struct VitalSignRowView: View {
    let vital: VitalSignInfo
    
    var body: some View {
        HStack {
            // Type Icon
            VitalTypeIconView(type: vital.type)
            
            VStack(alignment: .leading, spacing: 4) {
                Text(vital.type.displayName)
                    .font(.headline)
                
                HStack {
                    Text(vital.timestamp.formatted(date: .omitted, time: .shortened))
                        .font(.caption)
                        .foregroundColor(.secondary)
                    
                    if !vital.isSynced {
                        Image(systemName: "icloud.and.arrow.up")
                            .font(.caption)
                            .foregroundColor(.orange)
                    }
                    
                    Text("• \(vital.source)")
                        .font(.caption)
                        .foregroundColor(.secondary)
                }
            }
            
            Spacer()
            
            VStack(alignment: .trailing, spacing: 4) {
                Text("\(vital.value, specifier: "%.1f")")
                    .font(.title2)
                    .fontWeight(.semibold)
                
                Text(vital.unit)
                    .font(.caption)
                    .foregroundColor(.secondary)
            }
        }
        .padding(.vertical, 4)
        .liquidGlassCardStyle()
    }
}

// MARK: - Vital Type Icon

struct VitalTypeIconView: View {
    let type: VitalSignType
    
    var body: some View {
        Image(systemName: type.iconName)
            .font(.title2)
            .foregroundColor(type.color)
            .frame(width: 40, height: 40)
            .background(type.color.opacity(0.1))
            .clipShape(Circle())
    }
}

// MARK: - Empty State

struct EmptyVitalSignsView: View {
    var body: some View {
        VStack(spacing: 20) {
            Image(systemName: "heart.text.square")
                .font(.system(size: 60))
                .foregroundColor(.gray)
            
            Text("No Vital Signs")
                .font(.title2)
                .fontWeight(.semibold)
            
            Text("Start recording your health data to see trends and insights.")
                .font(.subheadline)
                .foregroundColor(.secondary)
                .multilineTextAlignment(.center)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .padding()
    }
}

// MARK: - Add Vital Sign View

struct AddVitalSignView: View {
    @EnvironmentObject var engineWrapper: MobileEngineWrapper
    @Environment(\.dismiss) private var dismiss
    
    @State private var selectedType = VitalSignType.heartRate
    @State private var value = ""
    @State private var selectedDate = Date()
    @State private var source = "Manual"
    @State private var isRecording = false
    
    var body: some View {
        NavigationView {
            Form {
                Section("Vital Sign Details") {
                    Picker("Type", selection: $selectedType) {
                        ForEach(VitalSignType.allCases, id: \.self) { type in
                            Text(type.displayName).tag(type)
                        }
                    }
                    
                    HStack {
                        Text("Value")
                        Spacer()
                        TextField("Enter value", text: $value)
                            .keyboardType(.decimalPad)
                            .multilineTextAlignment(.trailing)
                        Text(selectedType.unit)
                            .foregroundColor(.secondary)
                    }
                    
                    DatePicker("Date & Time", selection: $selectedDate)
                    
                    Picker("Source", selection: $source) {
                        Text("Manual").tag("Manual")
                        Text("Device").tag("Device")
                        Text("HealthKit").tag("HealthKit")
                    }
                }
            }
            .navigationTitle("Add Vital Sign")
            .navigationBarTitleDisplayMode(.inline)
            .liquidGlassNavigationStyle()
            .toolbar {
                ToolbarItem(placement: .navigationBarLeading) {
                    Button("Cancel") {
                        dismiss()
                    }
                }
                
                ToolbarItem(placement: .navigationBarTrailing) {
                    Button("Save") {
                        Task { await saveVitalSign() }
                    }
                    .disabled(value.isEmpty || isRecording)
                    .liquidGlassButtonStyle()
                }
            }
        }
    }
    
    private func saveVitalSign() async {
        guard let doubleValue = Double(value) else { return }
        
        isRecording = true
        
        let vitalSign = VitalSignInput(
            type: selectedType,
            value: doubleValue,
            unit: selectedType.unit,
            source: source,
            timestamp: selectedDate
        )
        
        let success = await engineWrapper.recordVitalSign(vitalSign)
        
        await MainActor.run {
            isRecording = false
            if success {
                dismiss()
            }
        }
    }
}

// MARK: - Supporting Types

enum DateRange: CaseIterable {
    case day, week, month, threeMonths, year
    
    var displayName: String {
        switch self {
        case .day: return "Today"
        case .week: return "Week"
        case .month: return "Month"
        case .threeMonths: return "3 Months"
        case .year: return "Year"
        }
    }
    
    var dateRange: (from: Date, to: Date) {
        let calendar = Calendar.current
        let now = Date()
        
        switch self {
        case .day:
            return (calendar.startOfDay(for: now), now)
        case .week:
            return (calendar.date(byAdding: .day, value: -7, to: now) ?? now, now)
        case .month:
            return (calendar.date(byAdding: .month, value: -1, to: now) ?? now, now)
        case .threeMonths:
            return (calendar.date(byAdding: .month, value: -3, to: now) ?? now, now)
        case .year:
            return (calendar.date(byAdding: .year, value: -1, to: now) ?? now, now)
        }
    }
}

struct VitalSignGroup {
    let date: Date
    let vitals: [VitalSignInfo]
}

extension VitalSignType {
    var iconName: String {
        switch self {
        case .heartRate: return "heart.fill"
        case .spO2: return "lungs.fill"
        case .steps: return "figure.walk"
        case .calories: return "flame.fill"
        case .activeEnergy: return "bolt.fill"
        case .exerciseMinutes: return "figure.run"
        case .standHours: return "figure.stand"
        }
    }
    
    var color: Color {
        switch self {
        case .heartRate: return .red
        case .spO2: return .cyan
        case .steps: return .green
        case .calories: return .orange
        case .activeEnergy: return .yellow
        case .exerciseMinutes: return .mint
        case .standHours: return .blue
        }
    }
}

struct VitalSignsView_Previews: PreviewProvider {
    static var previews: some View {
        VitalSignsView()
            .environmentObject(MobileEngineWrapper())
    }
}