import Foundation

@MainActor
final class AddMedicationSheetViewModel: ObservableObject {
    @Published private(set) var searchResults: [DrugSearchResult] = []

    private let searchDrugs: SearchDrugsUseCase
    private let addMedication: AddMedicationUseCase

    init(searchDrugs: SearchDrugsUseCase, addMedication: AddMedicationUseCase) {
        self.searchDrugs = searchDrugs
        self.addMedication = addMedication
    }

    func search(query: String) async {
        guard query.count >= 3 else {
            searchResults = []
            return
        }

        var collected: [DrugSearchResult] = []
        var seen = Set<String>()

        for candidateQuery in DrugNameQueryBuilder.build(query) {
            let results = await searchDrugs(query: candidateQuery)
            for result in results {
                let key = result.rxCui.lowercased()
                if seen.insert(key).inserted {
                    collected.append(result)
                }
            }

            if !collected.isEmpty {
                break
            }
        }

        searchResults = collected
    }

    func clearResults() {
        searchResults = []
    }

    func add(input: AddMedicationInput) async -> OperationResult {
        await addMedication(input)
    }
}

@MainActor
final class CheckInteractionsSheetViewModel: ObservableObject {
    @Published var medication1: String = ""
    @Published var medication2: String = ""
    @Published var includeActiveMedications: Bool = true
    @Published private(set) var isChecking: Bool = false
    @Published private(set) var hasChecked: Bool = false
    @Published private(set) var interactions: [MedicationInteractionInfo] = []
    @Published var errorMessage: String?

    private let searchDrugs: SearchDrugsUseCase
    private let checkInteractions: CheckMedicationInteractionsUseCase

    init(searchDrugs: SearchDrugsUseCase, checkInteractions: CheckMedicationInteractionsUseCase) {
        self.searchDrugs = searchDrugs
        self.checkInteractions = checkInteractions
    }

    var canCheck: Bool {
        let first = medication1.trimmingCharacters(in: .whitespacesAndNewlines)
        let second = medication2.trimmingCharacters(in: .whitespacesAndNewlines)
        return !first.isEmpty && (includeActiveMedications || !second.isEmpty)
    }

    func resetForNewCheck() {
        medication1 = ""
        medication2 = ""
        includeActiveMedications = true
        isChecking = false
        hasChecked = false
        interactions = []
        errorMessage = nil
    }

    func runCheck(activeMedications: [MedicationInfo]) async {
        guard canCheck else { return }

        let first = medication1.trimmingCharacters(in: .whitespacesAndNewlines)
        let second = medication2.trimmingCharacters(in: .whitespacesAndNewlines)

        isChecking = true
        hasChecked = false
        errorMessage = nil
        interactions = []

        defer { isChecking = false }

        var rxCuis: [String] = []

        guard let med1 = await resolveBestMatch(first) else {
            errorMessage = "Could not find a medication match for '" + first + "'."
            return
        }

        rxCuis.append(med1.rxCui)

        if !second.isEmpty {
            guard let med2 = await resolveBestMatch(second) else {
                errorMessage = "Could not find a medication match for '" + second + "'."
                return
            }

            rxCuis.append(med2.rxCui)
        }

        if includeActiveMedications {
            rxCuis.append(contentsOf: activeMedications
                .compactMap { $0.rxCui?.trimmingCharacters(in: .whitespacesAndNewlines) }
                .filter { !$0.isEmpty })
        }

        let uniqueRxCuis = Self.distinctCaseInsensitive(rxCuis)
        guard uniqueRxCuis.count >= 2 else {
            errorMessage = "Need at least two medications with valid RxCUI to check interactions."
            return
        }

        interactions = await checkInteractions(rxCuis: uniqueRxCuis)
        hasChecked = true
    }

    private func resolveBestMatch(_ medicationName: String) async -> DrugSearchResult? {
        guard !medicationName.isEmpty else { return nil }

        var matches: [DrugSearchResult] = []
        var seen = Set<String>()

        for candidateQuery in DrugNameQueryBuilder.build(medicationName) {
            let queryMatches = await searchDrugs(query: candidateQuery)
            for match in queryMatches {
                let key = match.rxCui.lowercased()
                if seen.insert(key).inserted {
                    matches.append(match)
                }
            }

            if !matches.isEmpty {
                break
            }
        }

        guard !matches.isEmpty else { return nil }

        let normalizedInput = DrugNameQueryBuilder.normalizeForMatch(medicationName)

        return matches
            .sorted {
                let left = Self.matchScore(input: normalizedInput, candidate: DrugNameQueryBuilder.normalizeForMatch($0.name))
                let right = Self.matchScore(input: normalizedInput, candidate: DrugNameQueryBuilder.normalizeForMatch($1.name))
                if left == right {
                    return $0.name.count < $1.name.count
                }
                return left > right
            }
            .first
    }

    private static func matchScore(input: String, candidate: String) -> Int {
        let candidateValue = candidate.lowercased().trimmingCharacters(in: .whitespacesAndNewlines)
        if candidateValue == input { return 100 }
        if candidateValue.hasPrefix(input + " ") { return 90 }
        if candidateValue.contains(" " + input + " ") || candidateValue.hasSuffix(" " + input) { return 80 }
        if candidateValue.contains(input) { return 60 }

        let tokens = input.split(separator: " ").map(String.init)
        let overlap = tokens.filter { candidateValue.contains($0) }.count
        return overlap * 10
    }

    private static func distinctCaseInsensitive(_ values: [String]) -> [String] {
        var seen: Set<String> = []
        return values.filter {
            let normalized = $0.lowercased()
            if seen.contains(normalized) {
                return false
            }

            seen.insert(normalized)
            return true
        }
    }
}

private enum DrugNameQueryBuilder {
    static func build(_ input: String) -> [String] {
        var queries: [String] = []
        appendUnique(input.trimmingCharacters(in: .whitespacesAndNewlines), to: &queries)

        let withoutDiacritics = input
            .folding(options: .diacriticInsensitive, locale: .current)
            .trimmingCharacters(in: .whitespacesAndNewlines)
        appendUnique(withoutDiacritics, to: &queries)

        let cleaned = normalizeForMatch(withoutDiacritics)
        appendUnique(cleaned, to: &queries)

        let firstToken = cleaned.split(separator: " ").map(String.init).first
        if let firstToken, firstToken.count >= 4 {
            appendUnique(firstToken, to: &queries)

            if firstToken.hasSuffix("a") && firstToken.count > 6 {
                appendUnique(String(firstToken.dropLast()), to: &queries)
            }
        }

        return queries
    }

    static func normalizeForMatch(_ value: String) -> String {
        let folded = value.folding(options: .diacriticInsensitive, locale: .current)
        let lower = folded.lowercased()
        let allowed = lower.map { ch in
            ch.isLetter || ch.isNumber || ch == " " ? ch : " "
        }

        return String(allowed)
            .split(whereSeparator: { $0.isWhitespace })
            .joined(separator: " ")
    }

    private static func appendUnique(_ value: String, to queries: inout [String]) {
        guard !value.isEmpty else { return }
        if !queries.contains(where: { $0.caseInsensitiveCompare(value) == .orderedSame }) {
            queries.append(value)
        }
    }
}

