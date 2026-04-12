import SwiftUI

struct PermissionsOnboardingView: View {
    let onDone: () -> Void

    @EnvironmentObject private var engineWrapper: MobileEngineWrapper
    @State private var isRequesting = false
    @State private var appearedPhase = false

    private let permissions: [(icon: String, color: Color, title: String, description: String)] = [
        ("heart.fill",          Color(hue: 0.97, saturation: 0.7, brightness: 0.9),   "HealthKit",      "Sync heart rate, steps, SpO₂ and sleep data automatically."),
        ("bell.badge.fill",     Color(hue: 0.55, saturation: 0.7, brightness: 0.95),  "Notifications",  "Get alerts for background health data sync and important events."),
        ("location.fill",       Color(hue: 0.35, saturation: 0.7, brightness: 0.85),  "Location",       "Fetch real-time air quality and environment data for your city."),
    ]

    var body: some View {
        ZStack {
            MeshGradientBackground()

            VStack(spacing: 0) {
                Spacer(minLength: 60)

                // Icon + headline
                VStack(spacing: 14) {
                    Image(systemName: "checkmark.shield.fill")
                        .font(.system(size: 64))
                        .foregroundStyle(LiquidGlass.tealPrimary)
                        .shadow(color: LiquidGlass.tealPrimary.opacity(0.5), radius: 20)
                        .scaleEffect(appearedPhase ? 1 : 0.6)
                        .opacity(appearedPhase ? 1 : 0)
                        .animation(.spring(response: 0.6, dampingFraction: 0.7).delay(0.05), value: appearedPhase)

                    VStack(spacing: 6) {
                        Text("Allow Permissions")
                            .font(.system(size: 28, weight: .bold, design: .rounded))
                            .foregroundColor(.white)

                        Text("DigitalTwin needs a few permissions to provide real-time health monitoring.")
                            .font(.subheadline)
                            .foregroundColor(.white.opacity(0.6))
                            .multilineTextAlignment(.center)
                            .padding(.horizontal, 32)
                    }
                    .opacity(appearedPhase ? 1 : 0)
                    .offset(y: appearedPhase ? 0 : 10)
                    .animation(.easeOut(duration: 0.45).delay(0.15), value: appearedPhase)
                }

                Spacer(minLength: 40)

                // Permission rows
                VStack(spacing: 12) {
                    ForEach(Array(permissions.enumerated()), id: \.offset) { index, p in
                        permissionRow(icon: p.icon, color: p.color, title: p.title, description: p.description)
                            .opacity(appearedPhase ? 1 : 0)
                            .offset(y: appearedPhase ? 0 : 20)
                            .animation(.easeOut(duration: 0.4).delay(0.25 + Double(index) * 0.08), value: appearedPhase)
                    }
                }
                .padding(.horizontal, 20)

                Spacer(minLength: 40)

                // CTA
                VStack(spacing: 12) {
                    Button(action: requestPermissions) {
                        HStack(spacing: 10) {
                            if isRequesting {
                                ProgressView()
                                    .tint(.white)
                                    .scaleEffect(0.9)
                            } else {
                                Image(systemName: "checkmark.circle.fill")
                            }
                            Text(isRequesting ? "Requesting…" : "Grant Permissions")
                                .fontWeight(.semibold)
                        }
                        .frame(maxWidth: .infinity)
                        .frame(height: 52)
                    }
                    .liquidGlassButtonStyle()
                    .disabled(isRequesting)
                    .padding(.horizontal, 20)

                    Button("Skip for now") {
                        onDone()
                    }
                    .font(.subheadline)
                    .foregroundColor(.white.opacity(0.45))
                }
                .opacity(appearedPhase ? 1 : 0)
                .animation(.easeOut(duration: 0.4).delay(0.5), value: appearedPhase)

                Spacer(minLength: 40)

                Text("You can change these settings anytime in the iOS Settings app.")
                    .font(.caption2)
                    .foregroundColor(.white.opacity(0.3))
                    .multilineTextAlignment(.center)
                    .padding(.horizontal, 32)
                    .padding(.bottom, 24)
            }
        }
        .onAppear { appearedPhase = true }
    }

    // MARK: - Permission Row

    private func permissionRow(icon: String, color: Color, title: String, description: String) -> some View {
        HStack(spacing: 16) {
            ZStack {
                RoundedRectangle(cornerRadius: 12)
                    .fill(color.opacity(0.18))
                    .frame(width: 48, height: 48)
                Image(systemName: icon)
                    .font(.system(size: 20))
                    .foregroundColor(color)
            }

            VStack(alignment: .leading, spacing: 3) {
                Text(title)
                    .font(.system(size: 15, weight: .semibold))
                    .foregroundColor(.white)
                Text(description)
                    .font(.caption)
                    .foregroundColor(.white.opacity(0.55))
                    .fixedSize(horizontal: false, vertical: true)
            }

            Spacer(minLength: 0)
        }
        .padding(16)
        .background {
            RoundedRectangle(cornerRadius: 16)
                .fill(.white.opacity(0.06))
                .overlay(
                    RoundedRectangle(cornerRadius: 16)
                        .stroke(.white.opacity(0.08), lineWidth: 1)
                )
        }
    }

    // MARK: - Permission Requests

    private func requestPermissions() {
        isRequesting = true
        Task {
            // 1. HealthKit
            _ = await engineWrapper.requestHealthKitAuthorization()

            // 2. Notifications
            _ = await engineWrapper.backgroundSyncService.requestNotificationPermissions()

            // 3. Location — LocationManager is created fresh in EnvironmentView; we fire
            //    a standalone CLLocationManager request here so the prompt appears upfront.
            await requestLocationPermission()

            isRequesting = false
            onDone()
        }
    }

    @MainActor
    private func requestLocationPermission() async {
        // The single-use manager triggers the OS prompt then is discarded.
        // EnvironmentView's own LocationManager will honour the granted status.
        let _ = OneTimeLocationPermissionRequester()
        // Give the OS a moment to register the request before we discard the object
        try? await Task.sleep(for: .milliseconds(600))
    }
}

// MARK: - One-time location helper

import CoreLocation

private final class OneTimeLocationPermissionRequester: NSObject, CLLocationManagerDelegate {
    private let manager = CLLocationManager()
    override init() {
        super.init()
        manager.delegate = self
        if manager.authorizationStatus == .notDetermined {
            manager.requestWhenInUseAuthorization()
        }
    }
}
