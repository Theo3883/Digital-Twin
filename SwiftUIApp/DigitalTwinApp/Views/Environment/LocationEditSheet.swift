import SwiftUI

struct LocationEditSheet: View {
    @ObservedObject var locationManager: LocationManager
    @Binding var cityText: String
    let onUseMyLocation: () -> Void
    let onApplyCity: (String) -> Void
    @Environment(\.dismiss) private var dismiss

    var body: some View {
        NavigationView {
            VStack(spacing: 20) {
                Button(action: onUseMyLocation) {
                    HStack {
                        Image(systemName: "location.fill")
                        Text("Use My Current Location")
                    }
                    .frame(maxWidth: .infinity)
                }
                .liquidGlassButtonStyle()

                Divider()

                TextField("Enter city name", text: $cityText)
                    .textFieldStyle(.roundedBorder)

                Button("Apply City") {
                    onApplyCity(cityText)
                }
                .liquidGlassButtonStyle()
                .disabled(cityText.isEmpty)

                Spacer()
            }
            .padding()
            .navigationTitle("Edit Location")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .cancellationAction) { Button("Cancel") { dismiss() } }
            }
        }
        .presentationDetents([.medium])
    }
}

