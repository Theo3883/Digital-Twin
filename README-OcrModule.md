# DigitalTwin.OCR Module

Local-only OCR module for the DigitalTwin MAUI app. Scans and imports medical documents, performs on-device OCR via Apple Vision, encrypts results with AES-GCM-256, stores them in a hardened local vault, and syncs sanitized metadata to PostgreSQL via the existing `ISyncDrainer` pattern.

---

## Architecture

```
DigitalTwin.MAUI                  (references OCR project)
  └── MainLayout.razor            <OcrSheet /> added here
  └── MauiProgram.cs              AddDigitalTwinOcr() call

DigitalTwin.OCR (Razor Class Library, net10.0;net10.0-ios;net10.0-maccatalyst)
  ├── Components/                 Blazor UI (OcrSheet, ScanPage, OcrResultPage, …)
  ├── Services/                   Business logic + iOS native APIs
  ├── Models/                     Domain models, enums
  ├── Policies/                   Pure business rules (no iOS deps)
  └── ViewModels/                 Orchestration layer

DigitalTwin.Domain                (OcrDocument model, IOcrDocumentRepository, OcrDocumentSyncDrainer)
DigitalTwin.Infrastructure        (OcrDocumentEntity, OcrDocumentRepository, EF migrations)
DigitalTwin.Composition           (registers OcrDocumentSyncDrainer as 8th ISyncDrainer)
DigitalTwin.OCR.Tests             (xUnit: policy, sanitizer, AES-GCM, sync record)
```

---

## Import sources

| Flow | API | Notes |
|------|-----|------|
| **Photos / gallery** | `PHPickerViewController` (`PhotosUI`) | User picks JPEG/PNG/HEIC/Live Photo stills; image is re-encoded to JPEG for the OCR pipeline. **No `NSPhotoLibraryUsageDescription` is required** for the modern read-only photo picker on iOS 14+. |
| **Files (PDF, images)** | `UIDocumentPickerViewController` | PDF, JPG, PNG from iCloud Drive / On My iPhone, etc. |

### Debugging photo import (gallery)

`PhotoLibraryImportService` writes detailed traces with the prefix **`[OCR PhotoLibrary]`**:

1. **`System.Diagnostics.Debug.WriteLine`** — shows in the IDE **Debug / Output** window when you run a **Debug** build (same process as the main MAUI app).
2. **`ILogger`** — category names start with `DigitalTwin.OCR`; in **DEBUG**, `MauiProgram.cs` sets `AddFilter("DigitalTwin.OCR", LogLevel.Debug)` so these lines appear in the same Debug output via `AddDebug()`.

If gallery import fails, search the output for `[OCR PhotoLibrary]` to see which step failed (registered UTIs, `LoadFileRepresentation`, `LoadObject`, `LoadDataRepresentation`, JPEG encode, quarantine write).

---

## iOS Configuration (DigitalTwin.MAUI)

### Info.plist additions

```xml
<!-- Camera access for VNDocumentCameraViewController -->
<key>NSCameraUsageDescription</key>
<string>DigitalTwin uses the camera to scan medical documents securely on-device.</string>

<!-- Face ID for vault unlock -->
<key>NSFaceIDUsageDescription</key>
<string>DigitalTwin uses Face ID to protect your scanned medical documents.</string>

<!-- Disable iTunes file sharing — vault must not be visible in Files app -->
<key>UIFileSharingEnabled</key>
<false/>

<!-- App does not expose documents via Files app -->
<key>LSSupportsOpeningDocumentsInPlace</key>
<false/>
```

### Entitlements additions

```xml
<!-- Apply NSFileProtectionComplete to all vault files -->
<key>com.apple.developer.default-data-protection</key>
<string>NSFileProtectionComplete</string>
```

---

## EF Core Migrations

### Local SQLite (applied automatically at app startup via `localDb.Database.Migrate()`)

The migration `20260322000000_AddOcrDocuments` creates the `OcrDocuments` table in local SQLite. It is applied automatically — no manual step required.

### Cloud PostgreSQL

Run once per deployment from your CI or developer machine:

```bash
cd DigitalTwin.Infrastructure

# Apply the cloud migration (requires CLOUD_CONNECTION_STRING env var)
dotnet ef database update 20260322000000_AddOcrDocumentsCloud \
  --context CloudDbContext \
  --connection "$CLOUD_CONNECTION_STRING"
```

Or use the fully automatic approach if your deployment already runs cloud migrations on startup — add `cloudDb.Database.Migrate()` to `ApplyDatabaseMigrations` in `MauiProgram.cs` (currently disabled by design — see comment in that method).

---

## Vault Security Model

| Property | Value |
|---|---|
| Location | `{AppDataDirectory}/Library/Application Support/DigitalTwin.OcrVault/` |
| iCloud Backup | Excluded (`NSURLIsExcludedFromBackupKey = true`) |
| File Protection | `NSFileProtectionComplete` (files locked when device is locked) |
| Encryption | AES-GCM-256 (BCL `System.Security.Cryptography.AesGcm`) |
| Key Storage | iOS Keychain, `kSecAccessibleWhenPasscodeSetThisDeviceOnly` + `UserPresence` |
| Key derivation | 256-bit random DEK per document, wrapped with 256-bit master key |
| Nonce | 96-bit random per encryption operation |
| Tag | 128-bit AES-GCM authentication tag |
| Plaintext on disk | Never — temp files deleted immediately after normalization |

---

## Simulator Limitations

| Feature | Simulator | Physical Device |
|---|---|---|
| VNDocumentCameraViewController (camera scan) | ❌ Not available | ✅ |
| Vision OCR (VNRecognizeTextRequest) | ✅ Available | ✅ |
| Face ID / Touch ID (LAContext) | ⚠️ Simulated only | ✅ |
| Keychain with `UserPresence` constraint | ⚠️ Requires simulated passcode | ✅ |
| NSFileProtectionComplete | ⚠️ No enforcement | ✅ |

**On simulator**: Use `SecurityMode.RelaxedDebug` (automatically set in `#if DEBUG` in `MauiProgram.cs`) which disables passcode and biometric requirements. The vault still initializes and all crypto paths run.

---

## Launching the OCR Sheet

Inject `IOcrSheetService` anywhere in the MAUI app:

```csharp
@inject IOcrSheetService OcrSheet

private async Task OpenOcr()
{
    var result = await OcrSheet.LaunchAsync(new OcrSessionRequest(
        Source: DocumentSourceType.Camera,
        PatientId: _currentPatientId));

    if (result.Status == OcrSessionStatus.Success)
    {
        // result.DocumentId, result.SanitizedPreview, result.OcrStatus available
        Snackbar.Add("Document scanned and encrypted.", Severity.Success);
    }
}
```

---

## Cloud Sync

OCR documents follow the existing **push/pull drainer** pattern:

1. `OcrSyncPreparationService.SaveAsync(record)` writes to local SQLite with `IsDirty = true`
2. `HealthDataSyncService` attempts an immediate cloud push
3. If offline: rows stay dirty, `OcrDocumentSyncDrainer` (Order = 8) syncs them in the next 5-minute drain cycle

**What reaches PostgreSQL:**
- `OpaqueInternalName`, `MimeType`, `PageCount`, `Sha256OfNormalized`, `SanitizedOcrPreview`, `ScannedAt`

**What stays local-only:**
- `EncryptedVaultPath` (always empty string in the cloud copy)
- Raw OCR text (never stored — only the sanitized preview is persisted)
- Encrypted file bytes (vault only)

---

## Doctor Portal TODOs (Future)

1. **Expose scanned documents in the doctor portal** — add a `DocumentsController` to `DigitalTwin.WebAPI` following the same `[Authorize(Roles="Doctor")]` pattern as `PatientsController`. Query the `OcrDocuments` table via the cloud `CloudDbContext`.

2. **App Attest** — if a patient-facing REST API endpoint is ever added, attach `DCAppAttestService` assertion headers to those requests. Currently out of scope since the MAUI app communicates directly with PostgreSQL (no HTTP endpoint to attest against).

3. **Key rotation** — implement a vault re-encryption flow triggered when `kSecAccessControlBiometryCurrentSet` detects a biometry change (new fingerprint / Face ID re-enroll). Hook into `UIApplicationDidBecomeActiveNotification`.
