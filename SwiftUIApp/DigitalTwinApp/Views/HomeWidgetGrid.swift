import Foundation
import SwiftUI

struct HomeWidgetGrid: View {
    let heartRate: Double
    let spO2: Double
    let steps: Double
    let envReading: EnvironmentReadingInfo?
    let sleepMinutes: Int
    let sleepQuality: Double
    let coachingAdvice: CoachingAdviceInfo?
    let hasProfile: Bool
    @Binding var selectedTab: Int
    @State private var showInsightDetails = false

    private var structuredInsightSections: [CoachingAdviceSectionInfo] {
        coachingAdvice?.sections ?? []
    }

    private var insightCategories: [String] {
        if !structuredInsightSections.isEmpty {
            return structuredInsightSections.map(\.title)
        }

        return extractInsightCategories(from: coachingAdvice?.advice)
    }

    private var insightCategoriesSubtitle: String {
        guard !insightCategories.isEmpty else {
            return "No categories detected yet."
        }

        return insightCategories.joined(separator: " • ")
    }

    private var learnMorePromptText: String {
        guard !insightCategories.isEmpty else {
            return "Press to learn more."
        }

        let previewCount = min(3, insightCategories.count)
        let preview = insightCategories.prefix(previewCount).joined(separator: ", ")
        let remaining = insightCategories.count - previewCount

        if remaining > 0 {
            return "Press to learn more about: \(preview), +\(remaining) more."
        }

        return "Press to learn more about: \(preview)."
    }

    private var insightDetailsText: String {
        guard let text = coachingAdvice?.advice,
              !text.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else {
            return "No coaching insight available yet."
        }

        return text
    }

    private var spO2Status: String {
        if spO2 >= 95 { return "Normal" }
        if spO2 >= 90 { return "Low" }
        return "Critical"
    }

    private var spO2StatusColor: Color {
        if spO2 >= 95 { return LiquidGlass.greenPositive }
        if spO2 >= 90 { return LiquidGlass.amberWarning }
        return LiquidGlass.redCritical
    }

    private var sleepQualityLabel: String {
        if sleepQuality >= 80 { return "Optimal" }
        if sleepQuality >= 60 { return "Fair" }
        return "Poor"
    }

    var body: some View {
        let columns = [GridItem(.flexible(), spacing: 12), GridItem(.flexible(), spacing: 12)]

        VStack(spacing: 12) {
            // 1. Heart Rate Hero Card (full width)
            heartRateCard

            // 2x2 grid: Steps, SpO2, Environment, Sleep
            LazyVGrid(columns: columns, spacing: 12) {
                stepsCard
                spO2Card
                environmentCard
                sleepCard
            }

            // AI Insight Hero (full width)
            aiInsightHeroCard
        }
    }

    // MARK: Heart Rate Hero

    private var heartRateCard: some View {
        ZStack(alignment: .bottom) {
            VStack(alignment: .leading, spacing: 12) {
                HStack {
                    ZStack {
                        Circle()
                            .fill(LiquidGlass.redCritical.opacity(0.2))
                            .frame(width: 32, height: 32)
                        Image(systemName: "heart.fill")
                            .font(.system(size: 14))
                            .foregroundColor(LiquidGlass.redCritical)
                    }
                    Text("Heart Rate")
                        .font(.subheadline)
                        .foregroundColor(.white.opacity(0.65))
                    Spacer()
                    if hasProfile {
                        Text("Live")
                            .font(.caption2.weight(.semibold))
                            .foregroundColor(LiquidGlass.greenPositive)
                            .padding(.horizontal, 8)
                            .padding(.vertical, 3)
                            .background {
                                RoundedRectangle(cornerRadius: LiquidGlass.radiusChip)
                                    .fill(LiquidGlass.greenPositive.opacity(0.15))
                            }
                    }
                }

                if hasProfile {
                    HStack(alignment: .firstTextBaseline, spacing: 4) {
                        Text(heartRate > 0 ? String(format: "%.0f", heartRate) : "--")
                            .font(.system(size: 56, weight: .bold, design: .rounded))
                            .foregroundColor(.white)
                        Text("BPM")
                            .font(.caption)
                            .foregroundColor(.white.opacity(0.4))
                    }

                    Text(heartRate > 0 ? "\(String(format: "%.0f", heartRate)) BPM · Live" : "Waiting for data…")
                        .font(.caption.weight(.medium))
                        .foregroundColor(LiquidGlass.redCritical.opacity(0.8))
                } else {
                    Button {
                        selectedTab = 4
                    } label: {
                        HStack(spacing: 8) {
                            Image(systemName: "person.crop.circle.badge.plus")
                                .font(.title2)
                                .foregroundColor(.white.opacity(0.4))
                            Text("Set up your patient profile")
                                .font(.subheadline)
                                .foregroundColor(.white.opacity(0.5))
                            Spacer()
                            Image(systemName: "chevron.right")
                                .font(.caption)
                                .foregroundColor(.white.opacity(0.3))
                        }
                    }
                    .padding(.vertical, 8)
                }
            }
            .frame(maxWidth: .infinity, alignment: .leading)
            .zIndex(1)

            // ECG sparkline decoration
            if hasProfile {
                EcgSparkline()
                    .frame(height: 96)
                    .opacity(0.3)
            }
        }
        .glassCard(tint: LiquidGlass.redCritical.opacity(0.08))
    }

    // MARK: Steps

    private var stepsCard: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack {
                ZStack {
                    Circle()
                        .fill(LiquidGlass.amberWarning.opacity(0.2))
                        .frame(width: 32, height: 32)
                    Image(systemName: "figure.walk")
                        .font(.system(size: 14))
                        .foregroundColor(LiquidGlass.amberWarning)
                }
                Spacer()
            }
            Text("Steps")
                .font(.caption)
                .foregroundColor(.white.opacity(0.65))
            Text(steps > 0 ? String(format: "%.0f", steps) : "--")
                .font(.title2.weight(.bold))
                .foregroundColor(.white)
            GeometryReader { geo in
                ZStack(alignment: .leading) {
                    Capsule()
                        .fill(.white.opacity(0.1))
                        .frame(height: 4)
                    Capsule()
                        .fill(LiquidGlass.amberWarning)
                        .frame(width: geo.size.width * min(steps / 10000, 1), height: 4)
                }
            }
            .frame(height: 4)
        }
        .frame(minHeight: 140)
        .glassCard()
    }

    // MARK: SpO2

    private var spO2Card: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack {
                ZStack {
                    Circle()
                        .fill(LiquidGlass.bluePrimary.opacity(0.2))
                        .frame(width: 32, height: 32)
                    Image(systemName: "lungs.fill")
                        .font(.system(size: 14))
                        .foregroundColor(LiquidGlass.bluePrimary)
                }
                Spacer()
            }
            Text("Blood Oxygen")
                .font(.caption)
                .foregroundColor(.white.opacity(0.65))
            Text(spO2 > 0 ? String(format: "%.1f%%", spO2) : "--")
                .font(.title2.weight(.bold))
                .foregroundColor(.white)
            Text(spO2 > 0 ? spO2Status : "No data")
                .font(.caption2)
                .foregroundColor(spO2 > 0 ? spO2StatusColor : .white.opacity(0.4))
        }
        .frame(minHeight: 140)
        .glassCard()
    }

    // MARK: Environment

    private var environmentCard: some View {
        VStack(alignment: .leading, spacing: 8) {
            HStack {
                ZStack {
                    Circle()
                        .fill(LiquidGlass.greenPositive.opacity(0.2))
                        .frame(width: 32, height: 32)
                    Image(systemName: "location.fill")
                        .font(.system(size: 14))
                        .foregroundColor(LiquidGlass.greenPositive)
                }
                Spacer()
            }
            Text(envReading?.locationDisplayName ?? "Unknown")
                .font(.caption)
                .foregroundColor(.white.opacity(0.65))
            if let temp = envReading?.temperature {
                Text(String(format: "%.0f°", temp))
                    .font(.title2.weight(.bold))
                    .foregroundColor(.white)
            } else {
                Text("--")
                    .font(.title2.weight(.bold))
                    .foregroundColor(.white.opacity(0.4))
            }
            if let aqi = envReading?.aqiIndex {
                Text("AQI \(aqi) · \(envReading?.airQualityDisplay ?? "")")
                    .font(.caption2)
                    .foregroundColor(.white.opacity(0.5))
            } else {
                Text("No data")
                    .font(.caption2)
                    .foregroundColor(.white.opacity(0.4))
            }
        }
        .frame(minHeight: 140)
        .glassCard(tint: LiquidGlass.greenPositive.opacity(0.08))
    }

    // MARK: Sleep

    private var sleepCard: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack {
                ZStack {
                    Circle()
                        .fill(Color(red: 99 / 255, green: 102 / 255, blue: 241 / 255).opacity(0.2))
                        .frame(width: 32, height: 32)
                    Image(systemName: "moon.fill")
                        .font(.system(size: 14))
                        .foregroundColor(LiquidGlass.purpleSleep)
                }
                Spacer()
            }
            Text("Sleep")
                .font(.caption)
                .foregroundColor(.white.opacity(0.65))
            if sleepMinutes > 0 {
                Text("\(sleepMinutes / 60)h \(sleepMinutes % 60)m")
                    .font(.title2.weight(.bold))
                    .foregroundColor(.white)
                Text(sleepQualityLabel)
                    .font(.caption2)
                    .foregroundColor(LiquidGlass.purpleSleep)
            } else {
                Text("--")
                    .font(.title2.weight(.bold))
                    .foregroundColor(.white.opacity(0.4))
                Text("No data")
                    .font(.caption2)
                    .foregroundColor(.white.opacity(0.4))
            }
        }
        .frame(minHeight: 140)
        .glassCard()
    }

    // MARK: AI Insight Hero

    private var aiInsightHeroCard: some View {
        Button {
            showInsightDetails = true
        } label: {
            HStack(spacing: 12) {
                ZStack {
                    Circle()
                        .fill(LiquidGlass.tealPrimary.opacity(0.15))
                        .frame(width: 36, height: 36)
                    Image(systemName: "sparkle")
                        .font(.system(size: 16))
                        .foregroundColor(LiquidGlass.tealPrimary)
                }

                VStack(alignment: .leading, spacing: 8) {
                    Text("MedAssist Insights")
                        .font(.subheadline.weight(.semibold))
                        .foregroundColor(.white)

                    Text(insightCategoriesSubtitle)
                        .font(.caption)
                        .foregroundColor(.white.opacity(0.8))
                        .lineSpacing(3)
                        .lineLimit(2)

                    Text(learnMorePromptText)
                        .font(.caption2)
                        .foregroundColor(.white.opacity(0.6))
                        .lineSpacing(3)
                        .lineLimit(2)
                }

                Spacer()

                Image(systemName: "chevron.right")
                    .font(.caption)
                    .foregroundColor(.white.opacity(0.3))
            }
            .frame(maxWidth: .infinity)
            .glassCard(tint: LiquidGlass.tealPrimary.opacity(0.06))
            .padding(.top, 0)
        }
        .buttonStyle(.plain)
        .sheet(isPresented: $showInsightDetails) {
            InsightDetailsSheet(
                categories: insightCategories,
                insightText: insightDetailsText,
                coachingAdvice: coachingAdvice
            )
            .presentationDetents([.medium, .large])
            .presentationDragIndicator(.visible)
        }
    }

    private func extractInsightCategories(from rawInsight: String?) -> [String] {
        guard let rawInsight,
              !rawInsight.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty,
              let regex = try? NSRegularExpression(pattern: #"\*\*(.+?)\*\*"#) else {
            return []
        }

        let nsText = rawInsight as NSString
        let range = NSRange(location: 0, length: nsText.length)
        let matches = regex.matches(in: rawInsight, options: [], range: range)

        var categories: [String] = []
        var seen: Set<String> = []

        for match in matches {
            guard match.numberOfRanges > 1 else { continue }
            let rawCategory = nsText.substring(with: match.range(at: 1))
            let category = rawCategory
                .replacingOccurrences(of: #"\s+"#, with: " ", options: .regularExpression)
                .trimmingCharacters(in: .whitespacesAndNewlines)

            guard !category.isEmpty else { continue }

            let key = category.lowercased()
            if key.contains("snapshot") {
                continue
            }

            if seen.insert(key).inserted {
                categories.append(category)
            }
        }

        return categories
    }
}

private struct InsightDetailsSheet: View {
    let categories: [String]
    let insightText: String
    let coachingAdvice: CoachingAdviceInfo?

    @Environment(\.dismiss) private var dismiss
    @State private var selectedCategory: String?

    private let topAnchorId = "insight-details-top"

    private struct InsightSection: Identifiable {
        let id: String
        let title: String
        let body: String
    }

    private var sectionedInsight: [InsightSection] {
        if let coachingAdvice, coachingAdvice.hasStructuredSections {
            return parseStructuredSections(from: coachingAdvice)
        }

        return parseSections(from: insightText)
    }

    private var displayedCategories: [String] {
        let parsedTitles = sectionedInsight.map(\.title)
        return parsedTitles.isEmpty ? categories : parsedTitles
    }

    var body: some View {
        NavigationStack {
            ZStack {
                LinearGradient(
                    colors: [
                        Color(red: 0.08, green: 0.10, blue: 0.20),
                        Color(red: 0.18, green: 0.08, blue: 0.24)
                    ],
                    startPoint: .topLeading,
                    endPoint: .bottomTrailing
                )
                .ignoresSafeArea()

                ScrollViewReader { proxy in
                    ScrollView(showsIndicators: false) {
                        VStack(alignment: .leading, spacing: 18) {
                            Color.clear
                                .frame(height: 0)
                                .id(topAnchorId)

                            if !displayedCategories.isEmpty {
                                VStack(alignment: .leading, spacing: 10) {
                                    Text("Categories")
                                        .font(.caption.weight(.semibold))
                                        .foregroundColor(LiquidGlass.tealPrimary)

                                    LazyVGrid(
                                        columns: [GridItem(.adaptive(minimum: 130), spacing: 8)],
                                        alignment: .leading,
                                        spacing: 8
                                    ) {
                                        ForEach(displayedCategories, id: \.self) { category in
                                            let isSelected = normalizedKey(for: selectedCategory) == normalizedKey(for: category)

                                            Button {
                                                selectedCategory = category
                                                withAnimation(.easeInOut(duration: 0.45)) {
                                                    proxy.scrollTo(anchorId(for: category), anchor: .top)
                                                }
                                            } label: {
                                                Text(category)
                                                    .font(.caption2.weight(.semibold))
                                                    .foregroundColor(.white.opacity(0.95))
                                                    .padding(.horizontal, 10)
                                                    .padding(.vertical, 6)
                                                    .background {
                                                        Capsule()
                                                            .fill(
                                                                isSelected
                                                                    ? LiquidGlass.tealPrimary.opacity(0.32)
                                                                    : LiquidGlass.tealPrimary.opacity(0.18)
                                                            )
                                                    }
                                                    .overlay {
                                                        Capsule()
                                                            .stroke(
                                                                isSelected
                                                                    ? LiquidGlass.tealPrimary.opacity(0.65)
                                                                    : LiquidGlass.tealPrimary.opacity(0.35),
                                                                lineWidth: 1
                                                            )
                                                    }
                                            }
                                            .buttonStyle(.plain)
                                        }
                                    }
                                }
                            }

                            if sectionedInsight.isEmpty {
                                MarkdownText(
                                    insightText,
                                    baseFont: .body,
                                    baseColor: .white.opacity(0.9),
                                    boldColor: LiquidGlass.tealPrimary,
                                    italicColor: .white.opacity(0.75)
                                )
                                .lineSpacing(7)
                                .fixedSize(horizontal: false, vertical: true)
                            } else {
                                ForEach(sectionedInsight) { section in
                                    VStack(alignment: .leading, spacing: 10) {
                                        Text(section.title)
                                            .font(.headline.weight(.semibold))
                                            .foregroundColor(LiquidGlass.tealPrimary)

                                        if !section.body.isEmpty {
                                            MarkdownText(
                                                section.body,
                                                baseFont: .body,
                                                baseColor: .white.opacity(0.9),
                                                boldColor: LiquidGlass.tealPrimary,
                                                italicColor: .white.opacity(0.75)
                                            )
                                            .lineSpacing(7)
                                            .fixedSize(horizontal: false, vertical: true)
                                        }
                                    }
                                    .id(section.id)
                                }
                            }
                        }
                        .padding(16)
                    }
                }
            }
            .navigationTitle("MedAssist Insights")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .topBarTrailing) {
                    Button("Close") {
                        dismiss()
                    }
                }
            }
        }
    }

    private func anchorId(for category: String) -> String {
        guard let matchedSection = sectionedInsight.first(where: {
            normalizedKey(for: $0.title) == normalizedKey(for: category)
        }) else {
            return topAnchorId
        }

        return matchedSection.id
    }

    private func parseStructuredSections(from advice: CoachingAdviceInfo) -> [InsightSection] {
        var sections: [InsightSection] = []

        let summaryTitle = advice.headline.isEmpty ? "Summary" : advice.headline
        if !advice.summary.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
            sections.append(
                InsightSection(
                    id: "insight-section-0-summary",
                    title: summaryTitle,
                    body: advice.summary
                )
            )
        }

        for section in advice.sections {
            let title = compactWhitespace(section.title)
            guard !title.isEmpty else { continue }

            let body = section.items
                .map { "• \($0)" }
                .joined(separator: "\n")
                .trimmingCharacters(in: .whitespacesAndNewlines)

            sections.append(
                InsightSection(
                    id: "insight-section-\(sections.count)-\(slug(from: title))",
                    title: title,
                    body: body
                )
            )
        }

        if !advice.actions.isEmpty {
            let actionsBody = advice.actions
                .map { "• \($0.label)" }
                .joined(separator: "\n")
            sections.append(
                InsightSection(
                    id: "insight-section-\(sections.count)-actions",
                    title: "Actions",
                    body: actionsBody
                )
            )
        }

        if !advice.motivation.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
            sections.append(
                InsightSection(
                    id: "insight-section-\(sections.count)-motivation",
                    title: "Motivation",
                    body: advice.motivation
                )
            )
        }

        if !advice.safetyNote.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
            sections.append(
                InsightSection(
                    id: "insight-section-\(sections.count)-safety",
                    title: "Safety",
                    body: advice.safetyNote
                )
            )
        }

        return sections
    }

    private func parseSections(from rawInsight: String) -> [InsightSection] {
        guard let regex = try? NSRegularExpression(pattern: #"\*\*(.+?)\*\*"#) else {
            return []
        }

        let nsText = rawInsight as NSString
        let fullRange = NSRange(location: 0, length: nsText.length)
        let matches = regex.matches(in: rawInsight, options: [], range: fullRange)

        guard !matches.isEmpty else {
            return []
        }

        var sections: [InsightSection] = []
        var seenKeys: Set<String> = []

        for (index, match) in matches.enumerated() {
            guard match.numberOfRanges > 1 else { continue }

            let rawTitle = nsText.substring(with: match.range(at: 1))
            let title = compactWhitespace(rawTitle)
            guard !title.isEmpty else { continue }

            if title.lowercased().contains("snapshot") {
                continue
            }

            let contentStart = match.range.location + match.range.length
            let contentEnd = index + 1 < matches.count ? matches[index + 1].range.location : nsText.length
            let contentLength = max(0, contentEnd - contentStart)
            let contentRange = NSRange(location: contentStart, length: contentLength)
            let rawBody = contentLength > 0 ? nsText.substring(with: contentRange) : ""
            let body = normalizeSectionBody(rawBody)

            let key = normalizedKey(for: title)
            if seenKeys.insert(key).inserted {
                sections.append(
                    InsightSection(
                        id: "insight-section-\(sections.count)-\(slug(from: title))",
                        title: title,
                        body: body
                    )
                )
            }
        }

        return sections
    }

    private func normalizeSectionBody(_ value: String) -> String {
        var body = value
            .replacingOccurrences(of: "\r\n", with: "\n")
            .trimmingCharacters(in: .whitespacesAndNewlines)

        body = body.replacingOccurrences(
            of: #"^\s*[-:]+\s*"#,
            with: "",
            options: .regularExpression
        )

        body = body.replacingOccurrences(
            of: #"\s*\|\s*"#,
            with: "\n",
            options: .regularExpression
        )

        body = body.replacingOccurrences(
            of: #"\s*-\s*•\s*"#,
            with: "\n• ",
            options: .regularExpression
        )

        body = body.replacingOccurrences(
            of: #"\n{3,}"#,
            with: "\n\n",
            options: .regularExpression
        )

        return body.trimmingCharacters(in: .whitespacesAndNewlines)
    }

    private func normalizedKey(for value: String?) -> String {
        guard let value else { return "" }
        return compactWhitespace(value).lowercased()
    }

    private func compactWhitespace(_ value: String) -> String {
        value
            .replacingOccurrences(of: #"\s+"#, with: " ", options: .regularExpression)
            .trimmingCharacters(in: .whitespacesAndNewlines)
    }

    private func slug(from value: String) -> String {
        let normalized = value
            .lowercased()
            .replacingOccurrences(of: #"[^a-z0-9]+"#, with: "-", options: .regularExpression)
            .trimmingCharacters(in: CharacterSet(charactersIn: "-"))

        return normalized.isEmpty ? "section" : normalized
    }
}
