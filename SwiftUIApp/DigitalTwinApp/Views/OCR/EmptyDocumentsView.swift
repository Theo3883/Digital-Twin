import SwiftUI

struct EmptyDocumentsView: View {
    var body: some View {
        VStack(spacing: 16) {
            Image(systemName: "doc.text.fill")
                .font(.system(size: 50))
                .foregroundColor(.white.opacity(0.3))
            Text("No Documents")
                .font(.title3)
                .fontWeight(.semibold)
                .foregroundColor(.white)
            Text("Scan or import medical documents to extract and organize your health records.")
                .font(.subheadline)
                .foregroundColor(.white.opacity(0.65))
                .multilineTextAlignment(.center)
        }
        .frame(maxWidth: .infinity)
        .padding(.vertical, 60)
    }
}

