using DigitalTwin.Mobile.Application.DTOs;
using DigitalTwin.Mobile.Domain.Enums;
using DigitalTwin.Mobile.Domain.Interfaces;
using DigitalTwin.Mobile.Domain.Models;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Mobile.Application.Services;

/// <summary>
/// Orchestrates pure-C# OCR text processing: classify → extract identity → validate → sanitize → extract structured → extract history.
/// Swift handles the actual Vision OCR and document scanning; this service processes the recognized text.
/// </summary>
public class OcrTextProcessingApplicationService
{
    private readonly IDocumentIdentityExtractor _identityExtractor;
    private readonly IDocumentIdentityValidator _identityValidator;
    private readonly ISensitiveDataSanitizer _sanitizer;
    private readonly IHeuristicFieldExtractor _fieldExtractor;
    private readonly IDocumentTypeClassifier _docTypeClassifier;
    private readonly IMedicalHistoryExtractor _historyExtractor;
    private readonly IOcrDocumentRepository _ocrRepo;
    private readonly IMedicalHistoryEntryRepository _historyRepo;
    private readonly IPatientRepository _patientRepo;
    private readonly IUserRepository _userRepo;
    private readonly MedicationApplicationService _medicationApp;
    private readonly ILogger<OcrTextProcessingApplicationService> _logger;

    public OcrTextProcessingApplicationService(
        IOcrDocumentRepository ocrRepo,
        IMedicalHistoryEntryRepository historyRepo,
        IPatientRepository patientRepo,
        IUserRepository userRepo,
        IDocumentTypeClassifier docTypeClassifier,
        IDocumentIdentityExtractor identityExtractor,
        IDocumentIdentityValidator identityValidator,
        ISensitiveDataSanitizer sanitizer,
        IHeuristicFieldExtractor fieldExtractor,
        IMedicalHistoryExtractor historyExtractor,
        MedicationApplicationService medicationApp,
        ILogger<OcrTextProcessingApplicationService> logger)
    {
        _identityExtractor = identityExtractor;
        _identityValidator = identityValidator;
        _sanitizer = sanitizer;
        _fieldExtractor = fieldExtractor;
        _docTypeClassifier = docTypeClassifier;
        _ocrRepo = ocrRepo;
        _historyRepo = historyRepo;
        _patientRepo = patientRepo;
        _userRepo = userRepo;
        _historyExtractor = historyExtractor;
        _medicationApp = medicationApp;
        _logger = logger;
    }

    /// <summary>Classify document type from raw OCR text.</summary>
    public string ClassifyDocument(string ocrText)
        => _docTypeClassifier.Classify(ocrText);

    /// <summary>Extract identity (name + CNP) from raw OCR text.</summary>
    public DocumentIdentity ExtractIdentity(string ocrText)
        => _identityExtractor.Extract(ocrText);

    /// <summary>Validate extracted identity against current patient.</summary>
    public async Task<IdentityValidationResult> ValidateIdentityAsync(string ocrText)
    {
        var identity = _identityExtractor.Extract(ocrText);
        var patient = await _patientRepo.GetCurrentPatientAsync();
        if (patient == null)
            return new IdentityValidationResult(false, false, false, "No patient profile found");

        var user = await _userRepo.GetByIdAsync(patient.UserId);
        var fullName = user != null ? $"{user.FirstName} {user.LastName}".Trim() : null;

        return _identityValidator.Validate(identity, fullName, patient.Cnp);
    }

    /// <summary>Sanitize raw OCR text (redact PII).</summary>
    public string SanitizeText(string ocrText)
        => _sanitizer.Sanitize(ocrText);

    /// <summary>Build sanitized preview from multiple pages.</summary>
    public string BuildSanitizedPreview(IEnumerable<string> pageTexts, int maxLength = 2000)
        => _sanitizer.BuildSanitizedPreview(pageTexts, maxLength);

    /// <summary>Extract structured fields from OCR text.</summary>
    public HeuristicExtractionResult ExtractStructured(string ocrText, string documentType)
        => _fieldExtractor.Extract(ocrText, documentType);

    /// <summary>Full processing pipeline: classify → extract → sanitize → structure → history.</summary>
    public async Task<OcrTextProcessingResult> ProcessFullAsync(string ocrText)
    {
        try
        {
            ocrText ??= string.Empty;
            _logger.LogInformation("[OCR] ═══ ProcessFullAsync START ═══  text length={Len}", ocrText.Length);

            // 1. Classify
            var docType = _docTypeClassifier.Classify(ocrText);
            _logger.LogInformation("[OCR] Step 1 — Classification: docType={DocType}", docType);

            // 2. Extract identity
            var identity = _identityExtractor.Extract(ocrText);
            _logger.LogInformation("[OCR] Step 2 — Identity: name={Name} (conf={NConf:F2}), cnp={Cnp} (conf={CConf:F2})",
                identity.ExtractedName ?? "(none)", identity.NameConfidence,
                identity.ExtractedCnp != null ? "***" + identity.ExtractedCnp[^4..] : "(none)", identity.CnpConfidence);

            // 3. Validate identity
            IdentityValidationResult? validation = null;
            var patient = await _patientRepo.GetCurrentPatientAsync();
            if (patient != null)
            {
                var user = await _userRepo.GetByIdAsync(patient.UserId);
                var fullName = user != null ? $"{user.FirstName} {user.LastName}".Trim() : null;
                validation = _identityValidator.Validate(identity, fullName, patient.Cnp);
                _logger.LogInformation("[OCR] Step 3 — Identity validation: valid={Valid}, nameMatch={Name}, cnpMatch={Cnp}, reason={Reason}",
                    validation.IsValid, validation.NameMatched, validation.CnpMatched, validation.Reason ?? "OK");
            }
            else
            {
                _logger.LogWarning("[OCR] Step 3 — No patient profile found, skipping identity validation");
            }

            // 4. Sanitize
            var sanitized = _sanitizer.Sanitize(ocrText) ?? string.Empty;
            _logger.LogDebug("[OCR] Step 4 — Sanitized text: {Len} chars (original {OrigLen})", sanitized.Length, ocrText.Length);

            // 5. Extract structured fields
            var extraction = _fieldExtractor.Extract(ocrText, docType);
            _logger.LogInformation("[OCR] Step 5 — Structured extraction: patient={Patient}, date={Date}, doctor={Doctor}, diag={Diag}, meds={MedCount}",
                extraction.PatientName ?? "(none)", extraction.ReportDate ?? "(none)",
                extraction.DoctorName ?? "(none)", extraction.Diagnosis != null ? "yes" : "no",
                extraction.Medications.Count);

            // 6. Extract history items (now doc-type-aware)
            var historyItems = _historyExtractor.Extract(sanitized, docType);
            _logger.LogInformation("[OCR] Step 6 — History extraction: docType={DocType}, items={Count}", docType, historyItems.Count);
            for (int i = 0; i < historyItems.Count; i++)
            {
                var item = historyItems[i];
                _logger.LogDebug("[OCR]   Item[{Idx}]: title={Title}, med={Med}, dosage={Dose}, conf={Conf:F2}",
                    i, item.Title.Length > 60 ? item.Title[..60] + "…" : item.Title,
                    string.IsNullOrEmpty(item.MedicationName) ? "(none)" : item.MedicationName,
                    string.IsNullOrEmpty(item.Dosage) ? "(none)" : item.Dosage,
                    item.Confidence);
            }

            _logger.LogInformation("[OCR] ═══ ProcessFullAsync DONE ═══  type={Type}, identity={HasIdentity}, items={Count}",
                docType, identity.ExtractedName != null || identity.ExtractedCnp != null, historyItems.Count);

            return new OcrTextProcessingResult(docType, identity, validation, sanitized, extraction, historyItems);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OCR] ═══ ProcessFullAsync FAILED ═══");
            return new OcrTextProcessingResult("Unknown", null, null, ocrText, null, []);
        }
    }

    /// <summary>
    /// Save scanned document and auto-append medical history (MAUI parity: vault id, hash, path, consolidated history, notes, Rx auto-add).
    /// </summary>
    public async Task<OcrDocumentDto> SaveDocumentFromCaptureAsync(SaveOcrDocumentInput input)
    {
        _logger.LogInformation("[OCR] ═══ SaveDocumentFromCapture START ═══  docId={DocId}, mime={Mime}, pages={Pages}",
            input.DocumentId ?? "(new)", input.MimeType, input.PageCount);

        var patient = await _patientRepo.GetCurrentPatientAsync();
        if (patient == null)
        {
            _logger.LogError("[OCR] No patient profile — cannot save document");
            throw new InvalidOperationException("No patient profile");
        }

        var pages = input.PageTexts.ToList();
        var combinedText = string.Join("\n---\n", pages);

        var result = input.CachedProcessingResult ?? await ProcessFullAsync(combinedText);

        if (result.Validation is { IsValid: false })
        {
            _logger.LogWarning("[OCR] Save blocked: identity validation failed: {Reason}", result.Validation.Reason);
            throw new InvalidOperationException(result.Validation.Reason ?? "Identity validation failed");
        }

        var effectiveType = string.IsNullOrWhiteSpace(input.DocumentTypeOverride)
            ? result.DocumentType
            : input.DocumentTypeOverride!;

        var sanitized = _sanitizer.Sanitize(combinedText) ?? string.Empty;
        var preview = _sanitizer.BuildSanitizedPreview(pages) ?? string.Empty;
        var historyItems = _historyExtractor.Extract(sanitized, effectiveType);

        _logger.LogInformation("[OCR] Save pipeline: effectiveDocType={Type}, historyItems={Count}", effectiveType, historyItems.Count);

        var docId = Guid.TryParse(input.DocumentId, out var parsedId) ? parsedId : Guid.NewGuid();
        var opaqueName = !string.IsNullOrWhiteSpace(input.VaultOpaqueInternalName)
            ? input.VaultOpaqueInternalName!
            : input.OpaqueInternalName;

        var now = DateTime.UtcNow;
        var doc = new OcrDocument
        {
            Id = docId,
            PatientId = patient.Id,
            OpaqueInternalName = opaqueName,
            MimeType = input.MimeType,
            PageCount = input.PageCount,
            DocumentType = effectiveType,
            Sha256OfNormalized = input.Sha256OfNormalized ?? string.Empty,
            EncryptedVaultPath = input.EncryptedVaultPath ?? string.Empty,
            SanitizedOcrPreview = preview,
            ScannedAt = now,
            CreatedAt = now,
            UpdatedAt = now,
            IsDirty = true
        };

        await _ocrRepo.SaveAsync(doc);
        _logger.LogInformation("[OCR] Saved OcrDocument: id={Id}, type={Type}, vaultPath={Vault}",
            doc.Id, doc.DocumentType, string.IsNullOrEmpty(doc.EncryptedVaultPath) ? "(none)" : "set");

        var existingForDoc = await _historyRepo.GetBySourceDocumentIdAsync(doc.Id);
        if (!existingForDoc.Any())
            await AppendOcrHistoryParityAsync(patient, doc.Id, preview, effectiveType, historyItems, now);
        else
            _logger.LogInformation("[OCR] History already exists for doc {DocId} — skipping append.", doc.Id);

        return MapToDto(doc, patient.Id);
    }

    /// <summary>Legacy entry point — runs full pipeline (no cache). Prefer <see cref="SaveDocumentFromCaptureAsync"/> from Swift.</summary>
    public Task<OcrDocumentDto> SaveDocumentAndExtractHistoryAsync(
        string opaqueInternalName,
        string mimeType,
        int pageCount,
        IEnumerable<string> pageTexts)
    {
        return SaveDocumentFromCaptureAsync(new SaveOcrDocumentInput
        {
            OpaqueInternalName = opaqueInternalName,
            MimeType = mimeType,
            PageCount = pageCount,
            PageTexts = pageTexts.ToArray()
        });
    }

    private async Task AppendOcrHistoryParityAsync(
        Patient patient,
        Guid sourceDocumentId,
        string sanitizedPreview,
        string docTypeString,
        IReadOnlyList<ExtractedHistoryItem> parsed,
        DateTime now)
    {
        var docTypeEnum = ParseMedicalDocumentType(docTypeString);

        var consolidated = BuildConsolidatedEntry(patient.Id, sourceDocumentId, docTypeEnum, parsed, sanitizedPreview, now);
        await _historyRepo.SaveRangeAsync([consolidated]);

        var appendBlock = BuildPatientSummaryBlock(consolidated, parsed, sanitizedPreview, sourceDocumentId, now);
        patient.MedicalHistoryNotes = string.IsNullOrWhiteSpace(patient.MedicalHistoryNotes)
            ? appendBlock
            : $"{patient.MedicalHistoryNotes}\n\n{appendBlock}";
        patient.UpdatedAt = now;
        await _patientRepo.SaveAsync(patient);

        if (docTypeEnum == MedicalDocumentType.Prescription && parsed.Count > 0)
        {
            foreach (var item in parsed)
            {
                if (string.IsNullOrWhiteSpace(item.MedicationName)) continue;
                var add = new AddMedicationInput
                {
                    Name = item.MedicationName.Trim(),
                    Dosage = item.Dosage ?? string.Empty,
                    Frequency = item.Frequency,
                    Route = MedicationRoute.Oral,
                    Instructions = item.Notes,
                    StartDate = now
                };
                var (ok, err) = await _medicationApp.AddMedicationFromOcrAsync(add);
                if (!ok)
                    _logger.LogWarning("[OCR] Auto-add medication failed: {Name} — {Err}", item.MedicationName, err);
            }
        }

        _logger.LogInformation("[OCR] Appended consolidated history + patient notes for doc {DocId}", sourceDocumentId);
    }

    private static MedicalHistoryEntry BuildConsolidatedEntry(
        Guid patientId,
        Guid sourceDocumentId,
        MedicalDocumentType docType,
        IReadOnlyList<ExtractedHistoryItem> parsed,
        string? sanitizedPreview,
        DateTime now)
    {
        var title = BuildConsolidatedTitle(docType, parsed, sanitizedPreview);
        return new MedicalHistoryEntry
        {
            PatientId = patientId,
            SourceDocumentId = sourceDocumentId,
            Title = title,
            MedicationName = string.Join(", ", parsed.Select(p => p.MedicationName).Where(s => !string.IsNullOrWhiteSpace(s))),
            Dosage = parsed.Count > 0 ? string.Join("; ", parsed.Select(p => $"{p.MedicationName} {p.Dosage}".Trim())) : string.Empty,
            Frequency = parsed.Count > 0 ? string.Join("; ", parsed.Select(p => $"{p.MedicationName}: {p.Frequency}".Trim())) : string.Empty,
            Duration = parsed.Count > 0 ? string.Join("; ", parsed.Select(p => $"{p.MedicationName}: {p.Duration}".Trim())) : string.Empty,
            Notes = parsed.Count > 0
                ? string.Join("\n", parsed.Select(p => $"{p.MedicationName} {p.Dosage} — {p.Notes}".TrimEnd(' ', '—', ' ')))
                : BuildDocumentSnippet(sanitizedPreview),
            Summary = parsed.Count > 0
                ? string.Join(" | ", parsed.Select(p => p.Summary))
                : BuildDocumentSnippet(sanitizedPreview, 120),
            Confidence = parsed.Count > 0 ? parsed.Average(p => p.Confidence) : 0.5m,
            EventDate = now,
            CreatedAt = now,
            UpdatedAt = now,
            IsDirty = true
        };
    }

    private static string BuildConsolidatedTitle(MedicalDocumentType docType, IReadOnlyList<ExtractedHistoryItem> parsed, string? rawText)
    {
        var typeLabel = docType switch
        {
            MedicalDocumentType.Prescription => "Prescription",
            MedicalDocumentType.Referral => "Referral",
            MedicalDocumentType.LabResult => "Lab Result",
            MedicalDocumentType.Discharge => "Discharge Summary",
            MedicalDocumentType.MedicalCertificate => "Medical Certificate",
            MedicalDocumentType.ImagingReport => "Imaging Report",
            MedicalDocumentType.EcgReport => "ECG Report",
            MedicalDocumentType.OperativeReport => "Operative Report",
            MedicalDocumentType.ConsultationNote => "Consultation Note",
            MedicalDocumentType.GenericClinicForm => "Clinic Form",
            _ => "OCR Document"
        };

        if (parsed.Count > 0 && parsed.Any(p => !string.IsNullOrWhiteSpace(p.MedicationName)))
            return $"{typeLabel}: {string.Join(", ", parsed.Select(p => p.MedicationName).Where(s => !string.IsNullOrWhiteSpace(s)))}";

        var snippet = BuildDocumentSnippet(rawText, 60);
        return string.IsNullOrWhiteSpace(snippet) ? typeLabel : $"{typeLabel}: {snippet}";
    }

    private static string BuildDocumentSnippet(string? text, int maxLength = 300)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var lines = text
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(l => l.Length > 5)
            .Take(8);

        var snippet = string.Join(" ", lines);
        return snippet.Length > maxLength ? snippet[..maxLength].TrimEnd() + "…" : snippet;
    }

    private static string BuildPatientSummaryBlock(
        MedicalHistoryEntry entry,
        IReadOnlyList<ExtractedHistoryItem> parsed,
        string? sanitizedPreview,
        Guid sourceDocumentId,
        DateTime utcNow)
    {
        var header = $"[OCR Summary {utcNow:yyyy-MM-dd} | Doc {sourceDocumentId.ToString("N")[..8].ToUpperInvariant()}]";

        if (parsed.Count > 0)
        {
            var lines = parsed.Select(p => $"- {p.Summary}");
            return $"{header}\n{string.Join('\n', lines)}";
        }

        return $"{header}\n- {entry.Summary}";
    }

    private static MedicalDocumentType ParseMedicalDocumentType(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return MedicalDocumentType.Unknown;
        return s.Trim() switch
        {
            "Prescription" => MedicalDocumentType.Prescription,
            "Referral" => MedicalDocumentType.Referral,
            "LabResult" => MedicalDocumentType.LabResult,
            "Discharge" => MedicalDocumentType.Discharge,
            "MedicalCertificate" => MedicalDocumentType.MedicalCertificate,
            "ImagingReport" => MedicalDocumentType.ImagingReport,
            "EcgReport" => MedicalDocumentType.EcgReport,
            "OperativeReport" => MedicalDocumentType.OperativeReport,
            "ConsultationNote" => MedicalDocumentType.ConsultationNote,
            "GenericClinicForm" => MedicalDocumentType.GenericClinicForm,
            _ => MedicalDocumentType.Unknown
        };
    }

    private static OcrDocumentDto MapToDto(OcrDocument doc, Guid patientId) => new()
    {
        Id = doc.Id,
        PatientId = patientId,
        OpaqueInternalName = doc.OpaqueInternalName,
        MimeType = doc.MimeType ?? string.Empty,
        DocumentType = doc.DocumentType ?? "Unknown",
        PageCount = doc.PageCount,
        Sha256OfNormalized = doc.Sha256OfNormalized ?? string.Empty,
        EncryptedVaultPath = doc.EncryptedVaultPath ?? string.Empty,
        SanitizedOcrPreview = doc.SanitizedOcrPreview ?? string.Empty,
        ScannedAt = doc.ScannedAt,
        CreatedAt = doc.CreatedAt,
        UpdatedAt = doc.UpdatedAt,
        IsDirty = doc.IsDirty,
        SyncedAt = doc.SyncedAt
    };
}
