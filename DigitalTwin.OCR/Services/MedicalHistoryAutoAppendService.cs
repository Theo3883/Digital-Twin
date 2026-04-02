using DigitalTwin.Application.DTOs;
using DigitalTwin.Application.Interfaces;
using DigitalTwin.Domain.Enums;
using DigitalTwin.Domain.Interfaces.Repositories;
using DigitalTwin.Domain.Models;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.OCR.Services;

/// <summary>
/// Auto-appends structured medical history entries from OCR output.
/// Keeps detailed entries for doctor-facing history and appends concise timeline text to patient notes.
/// For prescriptions, also adds each extracted medication to the patient's active medication list.
/// </summary>
public sealed class MedicalHistoryAutoAppendService
{
    private readonly IPatientRepository _patientRepository;
    private readonly IMedicalHistoryEntryRepository _historyRepository;
    private readonly MedicalHistoryExtractionService _extractor;
    private readonly DocumentTypeClassifierService _classifier;
    private readonly IMedicationApplicationService _medicationService;
    private readonly ILogger<MedicalHistoryAutoAppendService> _logger;

    public MedicalHistoryAutoAppendService(
        IPatientRepository patientRepository,
        IMedicalHistoryEntryRepository historyRepository,
        MedicalHistoryExtractionService extractor,
        DocumentTypeClassifierService classifier,
        IMedicationApplicationService medicationService,
        ILogger<MedicalHistoryAutoAppendService> logger)
    {
        _patientRepository = patientRepository;
        _historyRepository = historyRepository;
        _extractor = extractor;
        _classifier = classifier;
        _medicationService = medicationService;
        _logger = logger;
    }

    public async Task AppendAsync(Guid userId, Guid sourceDocumentId, string? sanitizedPreview, CancellationToken ct = default)
    {
        var patient = await _patientRepository.GetByUserIdAsync(userId);
        if (patient is null)
        {
            _logger.LogWarning("[OCR History] Patient not found for user {UserId}.", userId);
            return;
        }

        var existingFromSource = await _historyRepository.GetBySourceDocumentAsync(sourceDocumentId);
        if (existingFromSource.Any())
        {
            _logger.LogInformation("[OCR History] Entries already exist for source doc {DocId}.", sourceDocumentId);
            return;
        }

        var parsed = _extractor.Extract(sanitizedPreview);
        _logger.LogInformation("[OCR History] Extracted {Count} medication item(s) from doc {DocId}. DocType={DocType}.",
            parsed.Count, sourceDocumentId, _classifier.Classify(sanitizedPreview));

        var now = DateTime.UtcNow;
        var docType = _classifier.Classify(sanitizedPreview);

        // Build a single consolidated medical history entry for the entire document.
        // Always created — even when no structured medications were extracted (e.g. discharge letters, referrals).
        var consolidatedEntry = new MedicalHistoryEntry
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            SourceDocumentId = sourceDocumentId,
            Title = BuildConsolidatedTitle(docType, parsed, sanitizedPreview),
            MedicationName = string.Join(", ", parsed.Select(p => p.MedicationName)),
            Dosage = parsed.Count > 0 ? string.Join("; ", parsed.Select(p => $"{p.MedicationName} {p.Dosage}")) : string.Empty,
            Frequency = parsed.Count > 0 ? string.Join("; ", parsed.Select(p => $"{p.MedicationName}: {p.Frequency}")) : string.Empty,
            Duration = parsed.Count > 0 ? string.Join("; ", parsed.Select(p => $"{p.MedicationName}: {p.Duration}")) : string.Empty,
            Notes = parsed.Count > 0
                ? string.Join("\n", parsed.Select(p => $"{p.MedicationName} {p.Dosage} — {p.Notes}".TrimEnd(' ', '—', ' ')))
                : BuildDocumentSnippet(sanitizedPreview),
            Summary = parsed.Count > 0
                ? string.Join(" | ", parsed.Select(p => p.Summary))
                : BuildDocumentSnippet(sanitizedPreview, maxLength: 120),
            Confidence = parsed.Count > 0 ? parsed.Average(p => p.Confidence) : 0.5m,
            EventDate = now,
            CreatedAt = now,
            UpdatedAt = now,
            IsDirty = true
        };

        await _historyRepository.AddRangeAsync([consolidatedEntry]);

        var appendBlock = BuildPatientSummaryBlock(consolidatedEntry, parsed, sanitizedPreview, sourceDocumentId, now);
        patient.MedicalHistoryNotes = string.IsNullOrWhiteSpace(patient.MedicalHistoryNotes)
            ? appendBlock
            : $"{patient.MedicalHistoryNotes}\n\n{appendBlock}";
        patient.UpdatedAt = now;
        await _patientRepository.UpdateAsync(patient);

        // Only auto-add to active medications when the document is a prescription with extracted items.
        if (docType == MedicalDocumentType.Prescription && parsed.Count > 0)
        {
            await AutoAddMedicationsAsync(patient.Id, parsed, ct);
        }
    }

    private static string BuildConsolidatedTitle(MedicalDocumentType docType, IReadOnlyList<ExtractedHistoryItem> parsed, string? rawText)
    {
        var typeLabel = docType switch
        {
            MedicalDocumentType.Prescription => "Prescription",
            MedicalDocumentType.Referral => "Referral",
            MedicalDocumentType.LabResult => "Lab Result",
            MedicalDocumentType.Discharge => "Discharge Summary",
            _ => "OCR Document"
        };

        if (parsed.Count > 0)
            return $"{typeLabel}: {string.Join(", ", parsed.Select(p => p.MedicationName))}";

        // No structured meds — use a short snippet from the raw text as the subtitle.
        var snippet = BuildDocumentSnippet(rawText, maxLength: 60);
        return string.IsNullOrWhiteSpace(snippet) ? typeLabel : $"{typeLabel}: {snippet}";
    }

    private static string BuildDocumentSnippet(string? text, int maxLength = 300)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        // Take the first non-empty lines, skip header-like lines (ALL CAPS short tokens).
        var lines = text
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(l => l.Length > 5)
            .Take(8);

        var snippet = string.Join(" ", lines);
        return snippet.Length > maxLength ? snippet[..maxLength].TrimEnd() + "…" : snippet;
    }

    private async Task AutoAddMedicationsAsync(
        Guid patientId,
        IReadOnlyList<ExtractedHistoryItem> parsed,
        CancellationToken ct)
    {
        foreach (var item in parsed)
        {
            try
            {
                var dto = new AddMedicationDto
                {
                    Name = item.MedicationName,
                    Dosage = item.Dosage,
                    Frequency = item.Frequency,
                    Route = MedicationRoute.Oral,
                    Instructions = item.Notes,
                    StartDate = DateTime.UtcNow
                };

                await _medicationService.AddMedicationAsync(patientId, dto, AddedByRole.OcrScan, skipInteractionCheck: true);
                _logger.LogInformation("[OCR Meds] Auto-added '{Name}' for patient {PatientId}.", item.MedicationName, patientId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[OCR Meds] Failed to auto-add '{Name}' for patient {PatientId}.", item.MedicationName, patientId);
            }
        }
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

        // No structured medications — fall back to the consolidated entry summary.
        return $"{header}\n- {entry.Summary}";
    }
}

