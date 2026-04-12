import LocalAuthentication
import SwiftUI
import UIKit

enum OcrSheetPage: Int, CaseIterable {
    case scan
    case importOnly
    case result
}

/// Orchestrates OCR session state mirroring `OcrSessionViewModel` + Razor `OcrSheet` navigation.
@MainActor
final class OcrSessionController: ObservableObject {
    @Published var sheetPage: OcrSheetPage = .scan
    @Published var isProcessing = false
    @Published var processingResult: OcrProcessingResult?
    @Published var structuredResult: StructuredMedicalDocumentInfo?
    @Published var classificationResult: ClassificationResultInfo?
    @Published var identityMismatch: IdentityValidationInfo?
    @Published var reviewFlags: [ReviewFlagInfo] = []
    @Published var currentStep: OcrPipelineStep = .idle
    @Published var error: String?
    @Published var showScanner = false
    @Published var showPhotoPicker = false
    @Published var showFilePicker = false

    @Published var isLoadingVault = false
    @Published var statusMessage: String?
    @Published var vaultError: String?

    /// After successful save — shown on `OcrResultPageView`.
    @Published var lastSavedDocument: OcrDocumentInfo?

    /// Must be true to enable Camera / Photos / Files (after explicit unlock in this session).
    @Published private(set) var ocrSessionVaultUnlocked = false

    let keychainService = VaultKeychainService()
    private let visionService = OcrVisionService()
    private let pipelineDecoder: JSONDecoder = {
        let d = JSONDecoder()
        d.dateDecodingStrategy = .iso8601
        return d
    }()

    private var securityMode: OcrSecurityMode = .strict

    /// Call when the OCR sheet is presented: lock engine vault so user must unlock again (MAUI parity).
    func onOcrSheetAppear(repository: OcrRepository) async {
        _ = await repository.vaultLock()
        ocrSessionVaultUnlocked = false
        vaultError = nil
        refreshDevicePosture()
    }

    /// Call when user taps Import Documents — forces gate before sheet content (optional extra reset).
    func resetSessionGate() {
        ocrSessionVaultUnlocked = false
    }

    func currentPosture() -> OcrSecurityPosture {
        let ctx = LAContext()
        let passcodeSet = ctx.canEvaluatePolicy(.deviceOwnerAuthentication, error: nil)
        var bioErr: NSError?
        let bioAvailable = ctx.canEvaluatePolicy(.deviceOwnerAuthenticationWithBiometrics, error: &bioErr)
        let bioType = ctx.biometryType
        let bioLabel: String = switch bioType {
        case .faceID: "FaceID"
        case .touchID: "TouchID"
        case .opticID: "OpticID"
        case .none: "None"
        @unknown default: "None"
        }

        return OcrSecurityPosture(
            isPasscodeSet: passcodeSet,
            isBiometryAvailable: bioAvailable,
            biometryTypeLabel: bioLabel,
            isVaultInitialized: keychainService.keyExists,
            isVaultUnlocked: ocrSessionVaultUnlocked,
            activeMode: securityMode
        )
    }

    private func refreshDevicePosture() {
        objectWillChange.send()
    }

    func initializeVault(repository: OcrRepository) async {
        isLoadingVault = true
        statusMessage = "Initializing vault…"
        vaultError = nil
        print("[OCR Vault][Controller] initializeVault requested")
        defer {
            isLoadingVault = false
            statusMessage = nil
        }

        let ctx = LAContext()
        guard ctx.canEvaluatePolicy(.deviceOwnerAuthentication, error: nil) else {
            vaultError = "Device passcode is required."
            return
        }
        var bioLabel = "None"
        switch ctx.biometryType {
        case .faceID: bioLabel = "FaceID"
        case .touchID: bioLabel = "TouchID"
        case .opticID: bioLabel = "OpticID"
        default: break
        }
        let bioAvailable = ctx.canEvaluatePolicy(.deviceOwnerAuthenticationWithBiometrics, error: nil)

        guard !keychainService.keyExists else {
            vaultError = "Vault already initialized."
            print("[OCR Vault][Controller] initializeVault aborted: key already exists")
            return
        }

        let existingDocuments = await repository.loadDocuments()
        if !existingDocuments.isEmpty {
            vaultError = "Vault key is missing, but encrypted documents already exist. Reinitializing would make existing documents unreadable."
            print("[OCR Vault][Controller] initializeVault blocked: existing docs=\(existingDocuments.count), key missing")
            return
        }

        guard let masterKeyB64 = keychainService.generateAndStoreMasterKey() else {
            vaultError = "Failed to generate vault master key."
            print("[OCR Vault][Controller] initializeVault failed: generateAndStoreMasterKey returned nil")
            return
        }
        let input = VaultInitInput(
            isPasscodeSet: true,
            isBiometryAvailable: bioAvailable,
            biometryType: bioLabel,
            isVaultInitialized: false,
            isVaultUnlocked: false,
            activeMode: "Strict"
        )
        let initRes = await repository.vaultInitialize(input)
        guard let ir = initRes, ir.success else {
            vaultError = initRes?.error ?? "Vault initialization failed."
            print("[OCR Vault][Controller] initializeVault engine init failed: \(initRes?.error ?? "nil")")
            return
        }
        let unlockRes = await repository.vaultUnlock(masterKeyBase64: masterKeyB64)
        guard let ur = unlockRes, ur.success else {
            vaultError = unlockRes?.error ?? "Vault unlock failed."
            print("[OCR Vault][Controller] initializeVault unlock failed: \(unlockRes?.error ?? "nil")")
            return
        }
        ocrSessionVaultUnlocked = true
        print("[OCR Vault][Controller] initializeVault success: vault unlocked")
    }

    func unlockVault(repository: OcrRepository) async {
        isLoadingVault = true
        statusMessage = "Authenticating…"
        vaultError = nil
        print("[OCR Vault][Controller] unlockVault requested")
        defer {
            isLoadingVault = false
            statusMessage = nil
        }

        let ctx = LAContext()
        guard ctx.canEvaluatePolicy(.deviceOwnerAuthentication, error: nil) else {
            vaultError = "Device passcode is required. Enable it in Settings > Face ID & Passcode."
            return
        }

        var bioLabel = "None"
        switch ctx.biometryType {
        case .faceID: bioLabel = "FaceID"
        case .touchID: bioLabel = "TouchID"
        case .opticID: bioLabel = "OpticID"
        default: break
        }
        let bioAvailable = ctx.canEvaluatePolicy(.deviceOwnerAuthenticationWithBiometrics, error: nil)

        if !keychainService.keyExists {
            let existingDocuments = await repository.loadDocuments()
            if existingDocuments.isEmpty {
                vaultError = "Vault is not initialized. Initialize Secure Vault first."
            } else {
                vaultError = "Vault key is missing, but encrypted documents exist. Reinitializing would make existing documents unreadable."
            }
            print("[OCR Vault][Controller] unlockVault blocked: key missing, docs=\(existingDocuments.count), bioAvailable=\(bioAvailable), bioType=\(bioLabel)")
            return
        }

        guard let masterKeyB64 = await keychainService.retrieveMasterKey(
            reason: "Unlock your OCR vault"
        ) else {
            vaultError = "Authentication failed or was cancelled."
            print("[OCR Vault][Controller] unlockVault failed: retrieveMasterKey returned nil")
            return
        }
        guard let unlock = await repository.vaultUnlock(masterKeyBase64: masterKeyB64) else {
            vaultError = "Vault unlock failed."
            print("[OCR Vault][Controller] unlockVault failed: repository returned nil")
            return
        }
        guard unlock.success else {
            vaultError = unlock.error ?? "Vault unlock failed."
            print("[OCR Vault][Controller] unlockVault failed: \(unlock.error ?? "unknown")")
            return
        }
        ocrSessionVaultUnlocked = true
        print("[OCR Vault][Controller] unlockVault success")
    }

    func goToImportPage() {
        sheetPage = .importOnly
    }

    func backToScanPage() {
        sheetPage = .scan
    }

    func completeSessionAndDismiss() {
        resetForNewSession()
    }

    func resetForNewSession() {
        processingResult = nil
        structuredResult = nil
        classificationResult = nil
        identityMismatch = nil
        reviewFlags = []
        error = nil
        currentStep = .idle
        sheetPage = .scan
        lastSavedDocument = nil
    }

    // MARK: - Source triggers

    func startScan() {
        clearPipelineOnly()
        showScanner = true
    }

    func startPhotoPick() {
        clearPipelineOnly()
        showPhotoPicker = true
    }

    func startFileImport() {
        clearPipelineOnly()
        showFilePicker = true
    }

    private func clearPipelineOnly() {
        processingResult = nil
        structuredResult = nil
        classificationResult = nil
        identityMismatch = nil
        reviewFlags = []
        error = nil
        currentStep = .idle
        lastSavedDocument = nil
    }

    // MARK: - Handlers

    func handleScannedImages(_ images: [UIImage], repository: OcrRepository) {
        showScanner = false
        Task { await processImages(images, mimeType: "image/jpeg", repository: repository) }
    }

    func handlePickedPhoto(_ image: UIImage, _: Data, repository: OcrRepository) {
        showPhotoPicker = false
        Task { await processImages([image], mimeType: "image/jpeg", repository: repository) }
    }

    func handleImportedFile(_ result: ImportedFileResult, repository: OcrRepository) {
        showFilePicker = false
        Task { await processFileImport(result, repository: repository) }
    }

    // MARK: - Pipeline (from legacy coordinator)

    func processImages(_ images: [UIImage], mimeType: String, repository: OcrRepository) async {
        isProcessing = true
        error = nil
        identityMismatch = nil
        lastSavedDocument = nil
        defer { isProcessing = false }

        do {
            let documentId = UUID()
            let ocrT0 = CFAbsoluteTimeGetCurrent()
            print("[OCR Pipeline] Starting processImages: \(images.count) images, mimeType=\(mimeType), docId=\(documentId)")

            currentStep = .recognizing
            let pageTexts = try await visionService.recognizePages(images)
            print("[OCR Pipeline] Vision recognized \(pageTexts.count) pages, total chars=\(pageTexts.joined().count)")

            guard !pageTexts.allSatisfy({ $0.isEmpty }) else {
                error = "No text recognized in the scanned document."
                currentStep = .failed
                print("[OCR Pipeline] FAIL: all pages empty")
                return
            }

            let combinedText = pageTexts.joined(separator: "\n---\n")
            let ocrMs = Int64((CFAbsoluteTimeGetCurrent() - ocrT0) * 1000)
            print("[OCR Pipeline] OCR took \(ocrMs)ms, combined text \(combinedText.count) chars")

            var mlType: String?
            var mlConfidence: Float = 0
            if let ml = await DocumentTypeMlClassifier.classifyDocumentType(ocrText: combinedText) {
                mlType = ml.type
                mlConfidence = ml.confidence
                print("[OCR Pipeline] ML classification: type=\(ml.type), confidence=\(ml.confidence)")
            } else {
                print("[OCR Pipeline] ML classification returned nil")
            }

            currentStep = .classifying
            let classT0 = CFAbsoluteTimeGetCurrent()
            let classification = await repository.classifyWithOrchestrator(
                ocrText: combinedText, mlType: mlType, mlConfidence: mlConfidence
            )
            classificationResult = classification
            let classMs = Int64((CFAbsoluteTimeGetCurrent() - classT0) * 1000)
            print("[OCR Pipeline] Classification took \(classMs)ms → type=\(classification?.type ?? "nil"), conf=\(classification?.confidence ?? 0), method=\(classification?.method ?? "nil")")

            currentStep = .extractingIdentity
            guard let rawPipelineJson = await repository.processFullOcrRawJson(combinedText),
                  let jsonData = rawPipelineJson.data(using: .utf8) else {
                error = "OCR pipeline failed to return a result."
                currentStep = .failed
                print("[OCR Pipeline] FAIL: processFullOcrRawJson returned nil")
                return
            }
            print("[OCR Pipeline] Full pipeline JSON: \(rawPipelineJson.prefix(200))...")

            let result = try pipelineDecoder.decode(OcrProcessingResult.self, from: jsonData)
            processingResult = result
            print("[OCR Pipeline] Decoded result: docType=\(result.documentType ?? "nil"), identity name=\(result.identity?.extractedName ?? "nil")")

            currentStep = .validatingIdentity
            if let validation = result.validation, !validation.isValid {
                identityMismatch = validation
                error = validation.reason ?? "Identity on the document does not match your profile."
                currentStep = .failed
                print("[OCR Pipeline] FAIL: identity validation failed - \(validation.reason ?? "no reason")")
                return
            }
            print("[OCR Pipeline] Identity validation passed")

            currentStep = .encryptingAndStoring
            guard ocrSessionVaultUnlocked else {
                error = "Vault is not ready."
                currentStep = .failed
                print("[OCR Pipeline] FAIL: vault not unlocked")
                return
            }

            let vaultMime: String
            let vaultData: Data?
            if images.count > 1 {
                vaultMime = "application/pdf"
                vaultData = DocumentScanNormalizer.pdfDataFromScannedPages(images)
            } else if let one = images.first {
                vaultMime = mimeType
                vaultData = DocumentScanNormalizer.jpegData(one)
            } else {
                vaultMime = mimeType
                vaultData = nil
            }

            guard let payload = vaultData, !payload.isEmpty else {
                error = "Could not prepare document for encryption."
                currentStep = .failed
                print("[OCR Pipeline] FAIL: empty payload for vault")
                return
            }
            print("[OCR Pipeline] Vault payload ready: \(payload.count) bytes, mime=\(vaultMime)")

            let storeInput = VaultStoreDocumentInput(
                documentBase64: payload.base64EncodedString(),
                mimeType: vaultMime,
                pageCount: images.count,
                documentId: documentId.uuidString
            )
            let vaultStoreOutcome = await repository.vaultStoreDocument(storeInput)
            guard let vaultResult = vaultStoreOutcome, vaultResult.success else {
                error = vaultStoreOutcome?.error ?? "Failed to store document in the vault."
                currentStep = .failed
                print("[OCR Pipeline] FAIL: vault store failed - \(vaultStoreOutcome?.error ?? "nil")")
                return
            }
            print("[OCR Pipeline] Vault stored: opaqueName=\(vaultResult.opaqueInternalName ?? "nil"), sha256=\(vaultResult.sha256?.prefix(16) ?? "nil")")

            let docType = classification?.type ?? result.documentType
            let classConf = classification?.confidence ?? 0.5
            let classMethod = classification?.method ?? "keyword"

            currentStep = .buildingStructured
            let structuredInput = BuildStructuredDocumentInput(
                documentId: documentId.uuidString,
                ocrText: combinedText,
                docType: docType,
                classConfidence: classConf,
                classMethod: classMethod,
                useMlExtraction: true,
                ocrDurationMs: ocrMs,
                classificationDurationMs: classMs
            )
            let structured = await repository.buildStructuredDocumentFromJson(structuredInput)
            structuredResult = structured
            reviewFlags = structured?.reviewFlags ?? []
            print("[OCR Pipeline] Structured result: \(structured?.reviewFlags.count ?? 0) review flags")

            currentStep = .saving
            let save = SaveOcrDocumentInput(
                documentId: documentId.uuidString,
                opaqueInternalName: vaultResult.opaqueInternalName ?? documentId.uuidString.replacingOccurrences(of: "-", with: ""),
                mimeType: vaultMime,
                pageCount: images.count,
                pageTexts: pageTexts,
                encryptedVaultPath: vaultResult.vaultPath,
                sha256OfNormalized: vaultResult.sha256,
                vaultOpaqueInternalName: vaultResult.opaqueInternalName,
                documentTypeOverride: docType,
                cachedProcessingResultJson: rawPipelineJson
            )
            guard let saved = await repository.saveDocument(save) else {
                error = error ?? "Failed to save document record."
                currentStep = .failed
                print("[OCR Pipeline] FAIL: saveDocument returned nil")
                return
            }

            await repository.refreshAfterSave()
            lastSavedDocument = saved
            currentStep = .complete
            print("[OCR Pipeline] SUCCESS: document saved, id=\(saved.id), type=\(saved.documentType)")
        } catch {
            self.error = error.localizedDescription
            currentStep = .failed
            print("[OCR Pipeline] EXCEPTION: \(error)")
        }
    }

    func processFileImport(_ file: ImportedFileResult, repository: OcrRepository) async {
        isProcessing = true
        error = nil
        identityMismatch = nil
        lastSavedDocument = nil
        defer { isProcessing = false }
        print("[OCR Pipeline] processFileImport: mime=\(file.mimeType), size=\(file.fileSize), ext=\(file.fileExtension)")

        if file.mimeType.hasPrefix("image/") {
            guard let image = UIImage(data: file.data) else {
                error = "Unable to decode the imported image file."
                currentStep = .failed
                print("[OCR Pipeline] FAIL: cannot decode image file")
                return
            }
            await processImages([image], mimeType: file.mimeType, repository: repository)
            return
        }

        let documentId = UUID()
        let ocrT0 = CFAbsoluteTimeGetCurrent()
        print("[OCR Pipeline] File import: docId=\(documentId), starting validation")

        currentStep = .recognizing
        let headerBase64 = file.data.prefix(8).base64EncodedString()
        let validationResult = await repository.validateDocument(
            headerBase64: headerBase64,
            fileExtension: file.fileExtension,
            fileSizeBytes: file.fileSize
        )
        if let vr = validationResult, !vr.success {
            error = vr.error ?? "Document validation failed."
            currentStep = .failed
            return
        }

        let extracted = DocumentScanNormalizer.extractText(fromPdfData: file.data)
        let pageTexts: [String]
        if extracted.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
            pageTexts = ["[PDF — no selectable text]"]
        } else {
            pageTexts = [extracted]
        }

        let combinedText = pageTexts.joined(separator: "\n---\n")
        let ocrMs = Int64((CFAbsoluteTimeGetCurrent() - ocrT0) * 1000)

        let hasSelectablePdfText = !extracted.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
        print("[OCR Pipeline] File: PDF text extraction took \(ocrMs)ms, \(combinedText.count) chars, hasText=\(hasSelectablePdfText)")
        var fileMlType: String?
        var fileMlConfidence: Float = 0
        if hasSelectablePdfText, let ml = await DocumentTypeMlClassifier.classifyDocumentType(ocrText: combinedText) {
            fileMlType = ml.type
            fileMlConfidence = ml.confidence
        }

        currentStep = .classifying
        let classT0 = CFAbsoluteTimeGetCurrent()
        let classification = await repository.classifyWithOrchestrator(
            ocrText: combinedText, mlType: fileMlType, mlConfidence: fileMlConfidence
        )
        classificationResult = classification
        let classMs = Int64((CFAbsoluteTimeGetCurrent() - classT0) * 1000)
        print("[OCR Pipeline] File classification took \(classMs)ms → type=\(classification?.type ?? "nil")")

        currentStep = .extractingIdentity
        guard let rawPipelineJson = await repository.processFullOcrRawJson(combinedText),
              let jsonData = rawPipelineJson.data(using: .utf8) else {
            error = "OCR pipeline failed."
            currentStep = .failed
            print("[OCR Pipeline] FAIL: file import pipeline returned nil")
            return
        }

        do {
            let result = try pipelineDecoder.decode(OcrProcessingResult.self, from: jsonData)
            processingResult = result

            currentStep = .validatingIdentity
            if let validation = result.validation, !validation.isValid {
                identityMismatch = validation
                error = validation.reason ?? "Identity validation failed."
                currentStep = .failed
                return
            }

            currentStep = .encryptingAndStoring
            guard ocrSessionVaultUnlocked else {
                error = "Vault is not ready."
                currentStep = .failed
                return
            }

            let storeInput = VaultStoreDocumentInput(
                documentBase64: file.data.base64EncodedString(),
                mimeType: file.mimeType,
                pageCount: 1,
                documentId: documentId.uuidString
            )
            let vaultStoreOutcome = await repository.vaultStoreDocument(storeInput)
            guard let vaultResult = vaultStoreOutcome, vaultResult.success else {
                error = vaultStoreOutcome?.error ?? "Vault store failed."
                currentStep = .failed
                return
            }

            let docType = classification?.type ?? result.documentType
            let classConf = classification?.confidence ?? 0.5
            let classMethod = classification?.method ?? "keyword"

            currentStep = .buildingStructured
            let structuredInput = BuildStructuredDocumentInput(
                documentId: documentId.uuidString,
                ocrText: combinedText,
                docType: docType,
                classConfidence: classConf,
                classMethod: classMethod,
                useMlExtraction: true,
                ocrDurationMs: ocrMs,
                classificationDurationMs: classMs
            )
            structuredResult = await repository.buildStructuredDocumentFromJson(structuredInput)
            reviewFlags = structuredResult?.reviewFlags ?? []

            currentStep = .saving
            let save = SaveOcrDocumentInput(
                documentId: documentId.uuidString,
                opaqueInternalName: vaultResult.opaqueInternalName ?? documentId.uuidString.replacingOccurrences(of: "-", with: ""),
                mimeType: file.mimeType,
                pageCount: 1,
                pageTexts: pageTexts,
                encryptedVaultPath: vaultResult.vaultPath,
                sha256OfNormalized: vaultResult.sha256,
                vaultOpaqueInternalName: vaultResult.opaqueInternalName,
                documentTypeOverride: docType,
                cachedProcessingResultJson: rawPipelineJson
            )
            guard let saved = await repository.saveDocument(save) else {
                error = error ?? "Failed to save document."
                currentStep = .failed
                print("[OCR Pipeline] FAIL: file import save returned nil")
                return
            }

            await repository.refreshAfterSave()
            lastSavedDocument = saved
            currentStep = .complete
            print("[OCR Pipeline] SUCCESS: file import saved, id=\(saved.id), type=\(saved.documentType)")
        } catch {
            self.error = error.localizedDescription
            currentStep = .failed
            print("[OCR Pipeline] EXCEPTION in file import: \(error)")
        }
    }
}
