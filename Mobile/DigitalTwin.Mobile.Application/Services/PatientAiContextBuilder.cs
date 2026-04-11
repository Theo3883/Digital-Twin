using System.Text;
using DigitalTwin.Mobile.Domain.Enums;
using DigitalTwin.Mobile.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Mobile.Application.Services;

public sealed class PatientAiContextBuilder
{
    private static readonly TimeSpan VitalsLookback = TimeSpan.FromDays(7);
    private static readonly TimeSpan SleepLookback = TimeSpan.FromDays(14);

    private const int MaxVitalsPerType = 2;
    private const int MaxMedications = 8;
    private const int MaxSleepSessions = 5;
    private const int MaxHistoryEntries = 6;
    private const int MaxOcrDocuments = 3;
    private const int MaxPreviewLength = 140;

    private readonly IPatientRepository _patientRepository;
    private readonly IVitalSignRepository _vitalSignRepository;
    private readonly IMedicationRepository _medicationRepository;
    private readonly ISleepSessionRepository _sleepSessionRepository;
    private readonly IMedicalHistoryEntryRepository _medicalHistoryRepository;
    private readonly IOcrDocumentRepository _ocrDocumentRepository;
    private readonly ILogger<PatientAiContextBuilder> _logger;

    public PatientAiContextBuilder(
        IPatientRepository patientRepository,
        IVitalSignRepository vitalSignRepository,
        IMedicationRepository medicationRepository,
        ISleepSessionRepository sleepSessionRepository,
        IMedicalHistoryEntryRepository medicalHistoryRepository,
        IOcrDocumentRepository ocrDocumentRepository,
        ILogger<PatientAiContextBuilder> logger)
    {
        _patientRepository = patientRepository;
        _vitalSignRepository = vitalSignRepository;
        _medicationRepository = medicationRepository;
        _sleepSessionRepository = sleepSessionRepository;
        _medicalHistoryRepository = medicalHistoryRepository;
        _ocrDocumentRepository = ocrDocumentRepository;
        _logger = logger;
    }

    public async Task<string?> BuildChatContextAsync(CancellationToken ct = default)
    {
        return await BuildContextAsync(includeOcrPreviews: true, ct);
    }

    public async Task<string> BuildCoachingContextAsync(CancellationToken ct = default)
    {
        var context = await BuildContextAsync(includeOcrPreviews: false, ct);
        return string.IsNullOrWhiteSpace(context) ? "General health coaching" : context;
    }

    private async Task<string?> BuildContextAsync(bool includeOcrPreviews, CancellationToken ct)
    {
        var patient = await _patientRepository.GetCurrentPatientAsync();
        if (patient == null)
            return null;

        ct.ThrowIfCancellationRequested();

        var now = DateTime.UtcNow;
        var fromVitals = now.Subtract(VitalsLookback);
        var fromSleep = now.Subtract(SleepLookback);

        var vitalsTask = _vitalSignRepository.GetByPatientIdAsync(patient.Id, fromVitals, now);
        var medicationsTask = _medicationRepository.GetActiveByPatientIdAsync(patient.Id);
        var sleepTask = _sleepSessionRepository.GetByPatientIdAsync(patient.Id, fromSleep, now);
        var historyTask = _medicalHistoryRepository.GetByPatientIdAsync(patient.Id);
        var ocrTask = _ocrDocumentRepository.GetByPatientIdAsync(patient.Id);

        await Task.WhenAll(vitalsTask, medicationsTask, sleepTask, historyTask, ocrTask);

        var vitals = (await vitalsTask)
            .OrderByDescending(v => v.Timestamp)
            .ToList();
        var medications = (await medicationsTask)
            .OrderByDescending(m => m.UpdatedAt)
            .Take(MaxMedications)
            .ToList();
        var sleepSessions = (await sleepTask)
            .OrderByDescending(s => s.StartTime)
            .Take(MaxSleepSessions)
            .ToList();
        var historyEntries = (await historyTask)
            .OrderByDescending(h => h.EventDate)
            .Take(MaxHistoryEntries)
            .ToList();
        var ocrDocuments = (await ocrTask)
            .OrderByDescending(d => d.ScannedAt)
            .Take(MaxOcrDocuments)
            .ToList();

        var builder = new StringBuilder(1024);
        AppendProfileSection(builder, patient);
        AppendVitalsSection(builder, vitals);
        AppendMedicationSection(builder, medications);
        AppendSleepSection(builder, sleepSessions);
        AppendMedicalHistorySection(builder, historyEntries);
        AppendOcrSection(builder, ocrDocuments, includeOcrPreviews);

        _logger.LogInformation(
            "[PatientAiContext] Built context for patient {PatientId}. Vitals={VitalsCount}, Medications={MedicationCount}, SleepSessions={SleepCount}, History={HistoryCount}, OcrDocuments={OcrCount}",
            patient.Id,
            vitals.Count,
            medications.Count,
            sleepSessions.Count,
            historyEntries.Count,
            ocrDocuments.Count);

        return builder.ToString();
    }

    private static void AppendProfileSection(StringBuilder builder, Domain.Models.Patient patient)
    {
        builder.AppendLine("Patient Profile:");
        builder.AppendLine($"- BloodType: {ValueOrNA(patient.BloodType)}");
        builder.AppendLine($"- Allergies: {ValueOrNA(patient.Allergies)}");
        builder.AppendLine($"- MedicalHistoryNotes: {ValueOrNA(patient.MedicalHistoryNotes)}");
        builder.AppendLine($"- WeightKg: {ValueOrNA(patient.Weight)}");
        builder.AppendLine($"- HeightCm: {ValueOrNA(patient.Height)}");
        builder.AppendLine($"- BloodPressure: {FormatBloodPressure(patient.BloodPressureSystolic, patient.BloodPressureDiastolic)}");
        builder.AppendLine($"- Cholesterol: {ValueOrNA(patient.Cholesterol)}");
        builder.AppendLine($"- Cnp: {ValueOrNA(patient.Cnp)}");
        builder.AppendLine();
    }

    private static void AppendVitalsSection(StringBuilder builder, IReadOnlyCollection<Domain.Models.VitalSign> vitals)
    {
        builder.AppendLine("Recent Vitals (last 7 days):");
        if (vitals.Count == 0)
        {
            builder.AppendLine("- N/A");
            builder.AppendLine();
            return;
        }

        var grouped = vitals
            .GroupBy(v => v.Type)
            .OrderBy(g => g.Key);

        foreach (var group in grouped)
        {
            var latest = group
                .OrderByDescending(v => v.Timestamp)
                .Take(MaxVitalsPerType)
                .ToList();

            var values = string.Join(
                " | ",
                latest.Select(v => $"{v.Value:0.#} {v.Unit} @ {FormatTimestamp(v.Timestamp)}"));

            builder.AppendLine($"- {group.Key}: {values}");
        }

        builder.AppendLine();
    }

    private static void AppendMedicationSection(StringBuilder builder, IReadOnlyCollection<Domain.Models.Medication> medications)
    {
        builder.AppendLine("Active Medications:");
        if (medications.Count == 0)
        {
            builder.AppendLine("- None");
            builder.AppendLine();
            return;
        }

        foreach (var medication in medications)
        {
            builder.AppendLine(
                $"- {medication.Name}; Dosage={ValueOrNA(medication.Dosage)}; Frequency={ValueOrNA(medication.Frequency)}; Route={medication.Route}; Reason={ValueOrNA(medication.Reason)}; Status={medication.Status}");
        }

        builder.AppendLine();
    }

    private static void AppendSleepSection(StringBuilder builder, IReadOnlyCollection<Domain.Models.SleepSession> sessions)
    {
        builder.AppendLine("Sleep Sessions (last 14 days):");
        if (sessions.Count == 0)
        {
            builder.AppendLine("- N/A");
            builder.AppendLine();
            return;
        }

        var averageDuration = sessions.Average(s => s.DurationMinutes);
        var averageQuality = sessions.Average(s => s.QualityScore);
        builder.AppendLine($"- AverageDurationMinutes: {averageDuration:0}");
        builder.AppendLine($"- AverageQualityScore: {averageQuality:0.##}");

        foreach (var session in sessions)
        {
            builder.AppendLine(
                $"- Session: Start={FormatTimestamp(session.StartTime)}, End={FormatTimestamp(session.EndTime)}, DurationMinutes={session.DurationMinutes}, QualityScore={session.QualityScore:0.##}");
        }

        builder.AppendLine();
    }

    private static void AppendMedicalHistorySection(StringBuilder builder, IReadOnlyCollection<Domain.Models.MedicalHistoryEntry> entries)
    {
        builder.AppendLine("Medical History (latest):");
        if (entries.Count == 0)
        {
            builder.AppendLine("- N/A");
            builder.AppendLine();
            return;
        }

        foreach (var entry in entries)
        {
            builder.AppendLine(
                $"- {FormatTimestamp(entry.EventDate)} | Title={ValueOrNA(entry.Title)} | Medication={ValueOrNA(entry.MedicationName)} | Summary={TrimText(entry.Summary, MaxPreviewLength)} | Confidence={entry.Confidence:0.##}");
        }

        builder.AppendLine();
    }

    private static void AppendOcrSection(StringBuilder builder, IReadOnlyCollection<Domain.Models.OcrDocument> documents, bool includePreviews)
    {
        builder.AppendLine("OCR Documents (latest):");
        if (documents.Count == 0)
        {
            builder.AppendLine("- N/A");
            builder.AppendLine();
            return;
        }

        foreach (var document in documents)
        {
            var baseLine =
                $"- ScannedAt={FormatTimestamp(document.ScannedAt)}; MimeType={ValueOrNA(document.MimeType)}; PageCount={document.PageCount}";

            if (includePreviews)
            {
                var preview = TrimText(document.SanitizedOcrPreview, MaxPreviewLength);
                builder.AppendLine($"{baseLine}; Preview={ValueOrNA(preview)}");
            }
            else
            {
                builder.AppendLine(baseLine);
            }
        }

        builder.AppendLine();
    }

    private static string ValueOrNA(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "N/A" : value;
    }

    private static string ValueOrNA(double? value)
    {
        return value.HasValue ? value.Value.ToString("0.##") : "N/A";
    }

    private static string FormatBloodPressure(int? systolic, int? diastolic)
    {
        if (!systolic.HasValue || !diastolic.HasValue)
            return "N/A";

        return $"{systolic}/{diastolic}";
    }

    private static string TrimText(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "N/A";

        var trimmed = value.Trim();
        if (trimmed.Length <= maxLength)
            return trimmed;

        return string.Concat(trimmed.AsSpan(0, maxLength), "...");
    }

    private static string FormatTimestamp(DateTime timestamp)
    {
        return timestamp.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'");
    }
}
