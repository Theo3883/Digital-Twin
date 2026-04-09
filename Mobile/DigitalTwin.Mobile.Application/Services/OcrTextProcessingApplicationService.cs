using DigitalTwin.Mobile.Application.DTOs;
using DigitalTwin.Mobile.Domain.Interfaces;
using DigitalTwin.Mobile.Domain.Models;
using DigitalTwin.Mobile.Domain.Services;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Mobile.Application.Services;

/// <summary>
/// Orchestrates pure-C# OCR text processing: classify → extract identity → validate → sanitize → extract structured → extract history.
/// Swift handles the actual Vision OCR and document scanning; this service processes the recognized text.
/// </summary>
public class OcrTextProcessingApplicationService
{
    private readonly DocumentTypeClassifier _classifier = new();
    private readonly DocumentIdentityExtractor _identityExtractor = new();
    private readonly DocumentIdentityValidator _identityValidator = new();
    private readonly SensitiveDataSanitizer _sanitizer = new();
    private readonly HeuristicFieldExtractor _fieldExtractor = new();
    private readonly MedicalHistoryExtractor _historyExtractor = new();
    private readonly IOcrDocumentRepository _ocrRepo;
    private readonly IMedicalHistoryEntryRepository _historyRepo;
    private readonly IPatientRepository _patientRepo;
    private readonly IUserRepository _userRepo;
    private readonly ILogger<OcrTextProcessingApplicationService> _logger;

    public OcrTextProcessingApplicationService(
        IOcrDocumentRepository ocrRepo,
        IMedicalHistoryEntryRepository historyRepo,
        IPatientRepository patientRepo,
        IUserRepository userRepo,
        ILogger<OcrTextProcessingApplicationService> logger)
    {
        _ocrRepo = ocrRepo;
        _historyRepo = historyRepo;
        _patientRepo = patientRepo;
        _userRepo = userRepo;
        _logger = logger;
    }

    /// <summary>Classify document type from raw OCR text.</summary>
    public string ClassifyDocument(string ocrText)
        => DocumentTypeClassifier.Classify(ocrText);

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
            var docType = DocumentTypeClassifier.Classify(ocrText);
            var identity = _identityExtractor.Extract(ocrText);

            IdentityValidationResult? validation = null;
            var patient = await _patientRepo.GetCurrentPatientAsync();
            if (patient != null)
            {
                var user = await _userRepo.GetByIdAsync(patient.UserId);
                var fullName = user != null ? $"{user.FirstName} {user.LastName}".Trim() : null;
                validation = _identityValidator.Validate(identity, fullName, patient.Cnp);
            }

            var sanitized = _sanitizer.Sanitize(ocrText);
            var extraction = _fieldExtractor.Extract(ocrText, docType);
            var historyItems = _historyExtractor.Extract(sanitized);

            _logger.LogInformation("[OCR] Processed document: type={Type}, identity={HasIdentity}, items={Count}",
                docType, identity.ExtractedName != null || identity.ExtractedCnp != null, historyItems.Count);

            return new OcrTextProcessingResult(docType, identity, validation, sanitized, extraction, historyItems);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OCR] Full processing failed");
            return new OcrTextProcessingResult("Unknown", null, null, ocrText, null, []);
        }
    }

    /// <summary>
    /// Save scanned document and auto-append medical history entries.
    /// Called after Swift completes Vision OCR and passes recognized text.
    /// </summary>
    public async Task<OcrDocumentDto> SaveDocumentAndExtractHistoryAsync(
        string opaqueInternalName,
        string mimeType,
        int pageCount,
        IEnumerable<string> pageTexts)
    {
        var patient = await _patientRepo.GetCurrentPatientAsync();
        if (patient == null) throw new InvalidOperationException("No patient profile");

        var pages = pageTexts.ToList();
        var combinedText = string.Join("\n---\n", pages);

        // Full pipeline
        var result = await ProcessFullAsync(combinedText);

        // Save OCR document
        var doc = new OcrDocument
        {
            PatientId = patient.Id,
            OpaqueInternalName = opaqueInternalName,
            MimeType = mimeType,
            PageCount = pageCount,
            SanitizedOcrPreview = _sanitizer.BuildSanitizedPreview(pages),
            ScannedAt = DateTime.UtcNow,
            IsDirty = true
        };
        await _ocrRepo.SaveAsync(doc);

        // Append medical history entries
        var entries = new List<MedicalHistoryEntry>();
        foreach (var item in result.HistoryItems)
        {
            entries.Add(new MedicalHistoryEntry
            {
                PatientId = patient.Id,
                SourceDocumentId = doc.Id,
                Title = item.Title,
                MedicationName = item.MedicationName,
                Dosage = item.Dosage,
                Frequency = item.Frequency,
                Duration = item.Duration,
                Notes = item.Notes,
                Summary = item.Summary,
                Confidence = item.Confidence,
                EventDate = DateTime.UtcNow
            });
        }
        if (entries.Count > 0)
            await _historyRepo.SaveRangeAsync(entries);

        _logger.LogInformation("[OCR] Saved document {Id} with {Count} history items", doc.Id, result.HistoryItems.Count);

        return new OcrDocumentDto
        {
            Id = doc.Id,
            OpaqueInternalName = doc.OpaqueInternalName,
            MimeType = doc.MimeType,
            PageCount = doc.PageCount,
            SanitizedOcrPreview = doc.SanitizedOcrPreview,
            ScannedAt = doc.ScannedAt,
            IsDirty = doc.IsDirty
        };
    }
}
