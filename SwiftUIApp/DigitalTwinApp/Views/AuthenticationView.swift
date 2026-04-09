import SwiftUI

struct AuthenticationView: View {
    @EnvironmentObject var engineWrapper: MobileEngineWrapper
    @StateObject private var googleSignIn = GoogleSignInService()
    @State private var isAuthenticating = false
    @State private var showRegistrationForm = false
    @State private var googleIdToken: String?
    @State private var errorMessage: String?

    // Registration fields
    @State private var googleEmail = ""
    @State private var firstName = ""
    @State private var lastName = ""
    @State private var phone = ""
    @State private var dateOfBirth: Date?
    @State private var city = ""
    @State private var isCreatingAccount = false

    var body: some View {
        ScrollView {
            if !showRegistrationForm {
                // Sign-In Screen
                VStack(spacing: 40) {
                    Spacer(minLength: 80)

                    VStack(spacing: 16) {
                        Image(systemName: "heart.text.square.fill")
                            .font(.system(size: 80))
                            .foregroundStyle(LiquidGlass.tealPrimary)

                        Text("DigitalTwin")
                            .font(.largeTitle)
                            .fontWeight(.bold)
                            .foregroundColor(.white)

                        Text("Your Personal Health Companion")
                            .font(.headline)
                            .foregroundColor(.white.opacity(0.65))
                    }

                    Spacer(minLength: 60)

                    VStack(spacing: 16) {
                        Button(action: signInWithGoogle) {
                            HStack(spacing: 10) {
                                Image(systemName: "globe")
                                Text("Sign in with Google")
                                    .fontWeight(.semibold)
                            }
                            .frame(maxWidth: .infinity)
                            .frame(height: 50)
                        }
                        .liquidGlassButtonStyle()
                        .disabled(isAuthenticating)

                        if isAuthenticating {
                            ProgressView("Signing in...")
                                .foregroundColor(.white.opacity(0.65))
                                .frame(maxWidth: .infinity)
                        }

                        if let error = errorMessage {
                            Text(error)
                                .font(.caption)
                                .foregroundColor(LiquidGlass.redCritical)
                                .multilineTextAlignment(.center)
                        }
                    }

                    Spacer(minLength: 40)

                    Text("By signing in, you agree to our privacy policy and terms of service.")
                        .font(.caption)
                        .foregroundColor(.white.opacity(0.4))
                        .multilineTextAlignment(.center)
                        .padding(.horizontal)
                }
                .padding()
                .pageEnterAnimation()
            } else {
                // Registration Form
                VStack(spacing: 24) {
                    Spacer(minLength: 20)

                    Text("Complete Your Profile")
                        .font(.title2)
                        .fontWeight(.bold)
                        .foregroundColor(.white)

                    Text("Just a few more details to get started")
                        .font(.subheadline)
                        .foregroundColor(.white.opacity(0.65))

                    VStack(spacing: 16) {
                        // Email (read-only)
                        RegistrationField(icon: "envelope.fill", placeholder: "Email") {
                            TextField("Email", text: .constant(googleEmail))
                                .disabled(true)
                                .foregroundColor(.white.opacity(0.5))
                        }

                        // Name row
                        HStack(spacing: 12) {
                            RegistrationField(icon: "person.fill", placeholder: "First Name") {
                                TextField("First Name", text: $firstName)
                                    .foregroundColor(.white)
                            }
                            RegistrationField(icon: "person.fill", placeholder: "Last Name") {
                                TextField("Last Name", text: $lastName)
                                    .foregroundColor(.white)
                            }
                        }

                        // Phone
                        RegistrationField(icon: "phone.fill", placeholder: "Phone Number") {
                            TextField("Phone Number", text: $phone)
                                .keyboardType(.phonePad)
                                .foregroundColor(.white)
                        }

                        // Date of Birth
                        RegistrationField(icon: "calendar", placeholder: "Date of Birth") {
                            DatePicker(
                                "Date of Birth",
                                selection: Binding(
                                    get: { dateOfBirth ?? Date() },
                                    set: { dateOfBirth = $0 }
                                ),
                                in: ...Date(),
                                displayedComponents: .date
                            )
                            .labelsHidden()
                            .colorScheme(.dark)
                        }

                        // City
                        RegistrationField(icon: "mappin.circle.fill", placeholder: "City") {
                            TextField("City", text: $city)
                                .foregroundColor(.white)
                        }
                    }

                    VStack(spacing: 12) {
                        Button(action: createAccount) {
                            HStack {
                                if isCreatingAccount {
                                    ProgressView()
                                        .tint(.white)
                                }
                                Text("Create Account")
                                    .fontWeight(.semibold)
                            }
                            .frame(maxWidth: .infinity)
                            .frame(height: 50)
                        }
                        .liquidGlassButtonStyle()
                        .disabled(firstName.isEmpty || lastName.isEmpty || isCreatingAccount)

                        Button("Cancel") {
                            showRegistrationForm = false
                        }
                        .foregroundColor(.white.opacity(0.65))
                    }

                    Spacer(minLength: 40)
                }
                .padding()
                .pageEnterAnimation()
            }
        }
    }

    private func signInWithGoogle() {
        isAuthenticating = true
        errorMessage = nil

        Task {
            do {
                let idToken = try await googleSignIn.signIn()
                let success = await engineWrapper.authenticate(googleIdToken: idToken)

                isAuthenticating = false
                if success {
                    print("Authentication successful")
                } else {
                    // User needs to register — show form
                    googleIdToken = idToken
                    googleEmail = googleSignIn.userEmail ?? ""
                    firstName = googleSignIn.userGivenName ?? ""
                    lastName = googleSignIn.userFamilyName ?? ""
                    showRegistrationForm = true
                }
            } catch {
                isAuthenticating = false
                errorMessage = error.localizedDescription
            }
        }
    }

    private func createAccount() {
        isCreatingAccount = true

        Task {
            if let token = googleIdToken {
                let success = await engineWrapper.authenticate(googleIdToken: token)
                isCreatingAccount = false
                if !success {
                    errorMessage = "Failed to create account"
                }
            } else {
                isCreatingAccount = false
                errorMessage = "No authentication token available"
            }
        }
    }
}

