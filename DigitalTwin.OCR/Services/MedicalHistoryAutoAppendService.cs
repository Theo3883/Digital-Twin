using DigitalTwin.Domain.Interfaces.Repositories;
using DigitalTwin.Domain.Models;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.OCR.Services;

/// <summary>
/// Auto-appends structured medical history entries from OCR output.
/// Keeps detailed entries for doctor-facing history and appends concise timeline text to patient notes.
/// </summary>
public sealed class MedicalHistoryAutoAppendService
{
    private readonly IPatientRepository _patientRepository;
    private readonly IMedicalHistoryEntryRepository _historyRepository;
    private readonly MedicalHistoryExtractionService _extractor;
    private readonly ILogger<MedicalHistoryAutoAppendService> _logger;

    public MedicalHistoryAutoAppendService(
        IPatientRepository patientRepository,
        IMedicalHistoryEntryRepository historyRepository,
        MedicalHistoryExtractionService extractor,
        ILogger<MedicalHistoryAutoAppendService> logger)
    {
        _patientRepository = patientRepository;
        _historyRepository = historyRepository;
        _extractor = extractor;
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
        if (parsed.Count == 0)
            return;

        var now = DateTime.UtcNow;
        var entries = parsed.Select(p => new MedicalHistoryEntry
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            SourceDocumentId = sourceDocumentId,
            Title = p.Title,
            MedicationName = p.MedicationName,
            Dosage = p.Dosage,
            Frequency = p.Frequency,
            Duration = p.Duration,
            Notes = p.Notes,
            Summary = p.Summary,
            Confidence = p.Confidence,
            EventDate = now,
            CreatedAt = now,
            UpdatedAt = now,
            IsDirty = true
        }).ToList();

        await _historyRepository.AddRangeAsync(entries);

        var appendBlock = BuildPatientSummaryBlock(entries, sourceDocumentId, now);
        patient.MedicalHistoryNotes = string.IsNullOrWhiteSpace(patient.MedicalHistoryNotes)
            ? appendBlock
            : $"{patient.MedicalHistoryNotes}\n\n{appendBlock}";
        patient.UpdatedAt = now;
        await _patientRepository.UpdateAsync(patient);
    }

    private static string BuildPatientSummaryBlock(
        IReadOnlyList<MedicalHistoryEntry> entries,
        Guid sourceDocumentId,
        DateTime utcNow)
    {
        var header = $"[OCR Summary {utcNow:yyyy-MM-dd} | Doc {sourceDocumentId.ToString("N")[..8].ToUpperInvariant()}]";
        var lines = entries.Select(e => $"- {e.Summary}");
        return $"{header}\n{string.Join('\n', lines)}";
    }
}

