import SwiftUI

struct CriticalAlertBanner: View {
    let result: EcgEvaluationResult

    var body: some View {
        HStack {
            Image(systemName: "exclamationmark.triangle.fill")
                .font(.title2)
                .foregroundColor(.white)
            VStack(alignment: .leading) {
                Text("Critical Alert")
                    .font(.headline)
                    .foregroundColor(.white)
                Text(result.alerts.first ?? "Abnormal reading detected")
                    .font(.caption)
                    .foregroundColor(.white.opacity(0.9))
            }
            Spacer()
        }
        .glassBanner(tint: LiquidGlass.redCritical)
    }
}

