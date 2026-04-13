import SwiftUI

struct LocationEditSheet: View {
    @ObservedObject var locationManager: LocationManager
    @Binding var cityText: String
    let onUseMyLocation: () -> Void
    let onApplyCity: (String) -> Void
    @Environment(\.dismiss) private var dismiss
    @FocusState private var isCityFieldFocused: Bool

    private var trimmedCityText: String {
        cityText.trimmingCharacters(in: .whitespacesAndNewlines)
    }

    private func applyCityIfValid() {
        let city = trimmedCityText
        guard !city.isEmpty else { return }
        onApplyCity(city)
    }

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

                VStack(alignment: .leading, spacing: 8) {
                    Text("Enter City")
                        .font(.caption)
                        .foregroundColor(LiquidGlass.textSec)

                    HStack(spacing: 10) {
                        Image(systemName: "magnifyingglass")
                            .foregroundColor(isCityFieldFocused ? LiquidGlass.tealPrimary : LiquidGlass.textSec)

                        TextField(text: $cityText, prompt: Text("Enter city name").foregroundColor(LiquidGlass.textTert)) {
                            Text("City")
                        }
                        .focused($isCityFieldFocused)
                        .textInputAutocapitalization(.words)
                        .autocorrectionDisabled(true)
                        .submitLabel(.search)
                        .onSubmit(applyCityIfValid)
                        .foregroundColor(.white)
                        .tint(LiquidGlass.tealPrimary)
                    }
                    .glassInputBar(isFocused: isCityFieldFocused)
                }

                Button("Apply City") {
                    applyCityIfValid()
                }
                .liquidGlassButtonStyle()
                .disabled(trimmedCityText.isEmpty)

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

