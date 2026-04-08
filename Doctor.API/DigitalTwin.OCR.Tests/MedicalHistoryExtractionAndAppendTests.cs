using DigitalTwin.Application.DTOs;
using DigitalTwin.Application.Interfaces;
using DigitalTwin.Domain.Enums;
using DigitalTwin.Domain.Interfaces.Repositories;
using DigitalTwin.Domain.Models;
using DigitalTwin.OCR.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DigitalTwin.OCR.Tests;

public class MedicalHistoryExtractionAndAppendTests
{
    [Fact]
    public void Extract_ParsesMedicationLine_WithDosageFrequencyAndSummary()
    {
        var svc = new MedicalHistoryExtractionService();
        var text = "1. Aspenter 75 mg, se ia 1 cp/zi, dimineata, pe termen lung.";

        var result = svc.Extract(text);

        var item = Assert.Single(result);
        Assert.Equal("Aspenter", item.MedicationName);
        Assert.Contains("75 mg", item.Dosage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Morning", item.Frequency);
        Assert.Equal("Long term", item.Duration);
        Assert.Contains("Aspenter", item.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Extract_ParsesRpPrefixedLine()
    {
        var svc = new MedicalHistoryExtractionService();
        var text = "Rp.: 1. Aspenter 75 mg, dimineata";

        var result = svc.Extract(text);

        var item = Assert.Single(result);
        Assert.Equal("Aspenter", item.MedicationName);
        Assert.Contains("75 mg", item.Dosage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Extract_ParsesMultiWordDrugName()
    {
        var svc = new MedicalHistoryExtractionService();
        var text = "2. Betaloc ZOK 50mg, se ia 1 cp/zi.";

        var result = svc.Extract(text);

        var item = Assert.Single(result);
        Assert.Equal("Betaloc ZOK", item.MedicationName);
        Assert.Contains("50mg", item.Dosage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Extract_JoinsContinuationLines()
    {
        var svc = new MedicalHistoryExtractionService();
        var text = "1. Aspenter 75 mg, se ia 1 cp/zi, dimineata,\npe termen lung.";

        var result = svc.Extract(text);

        var item = Assert.Single(result);
        Assert.Equal("Long term", item.Duration);
    }

    [Fact]
    public void Extract_ParsesFullPrescription_ThreeDrugs()
    {
        var svc = new MedicalHistoryExtractionService();
        var text = @"Rp.: 1. Aspenter 75 mg, se ia 1 cp/zi, dimineata, pe termen lung.
2. Betaloc ZOK 50mg, se ia 1 cp/zi.
3. Rosuvastatina 10 mg, se ia 1 cp/zi, seara, continuu.";

        var result = svc.Extract(text);

        Assert.Equal(3, result.Count);
        Assert.Equal("Aspenter", result[0].MedicationName);
        Assert.Equal("Betaloc ZOK", result[1].MedicationName);
        Assert.Equal("Rosuvastatina", result[2].MedicationName);
    }

    [Fact]
    public async Task AutoAppend_IsIdempotent_BySourceDocumentId()
    {
        var (svc, patientRepo, historyRepo, _) = CreateAutoAppendService();

        var userId = Guid.NewGuid();
        var patient = new Patient { Id = Guid.NewGuid(), UserId = userId, MedicalHistoryNotes = "Baseline notes" };
        patientRepo.Upsert(patient);

        var docId = Guid.NewGuid();
        var preview = "1. Rosuvastatina 10 mg, se ia 1 cp/zi, seara, continuu.";

        await svc.AppendAsync(userId, docId, preview);
        await svc.AppendAsync(userId, docId, preview);

        var saved = (await historyRepo.GetBySourceDocumentAsync(docId)).ToList();
        Assert.Single(saved);

        var updated = await patientRepo.GetByUserIdAsync(userId);
        Assert.NotNull(updated);
        Assert.Contains("Rosuvastatina", updated!.MedicalHistoryNotes, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AutoAppend_DischargeDocument_CreatesHistoryEntry_EvenWithoutDosage()
    {
        var (svc, patientRepo, historyRepo, medSvc) = CreateAutoAppendService();

        var userId = Guid.NewGuid();
        var patient = new Patient { Id = Guid.NewGuid(), UserId = userId };
        patientRepo.Upsert(patient);

        // Discharge letter — drug names present but NO dosage values, so Extract() returns 0 items.
        var preview = "SCRISOARE MEDICALĂ\nDiagnostic: HTA gr. II\nRecomandări: Continuarea tratamentului (Atenolol, Enalapril).";
        var docId = Guid.NewGuid();

        await svc.AppendAsync(userId, docId, preview);

        // A history entry MUST be created regardless of whether medications are extracted.
        var saved = (await historyRepo.GetBySourceDocumentAsync(docId)).ToList();
        Assert.Single(saved);
        Assert.Contains("Discharge", saved[0].Title, StringComparison.OrdinalIgnoreCase);

        // No active medications auto-added (not a prescription).
        Assert.Empty(medSvc.Added);

        // Patient notes updated.
        var updated = await patientRepo.GetByUserIdAsync(userId);
        Assert.NotNull(updated?.MedicalHistoryNotes);
    }

    [Fact]
    public async Task AutoAppend_Prescription_AutoAddsMedications()
    {
        var (svc, patientRepo, _, medSvc) = CreateAutoAppendService();

        var userId = Guid.NewGuid();
        var patient = new Patient { Id = Guid.NewGuid(), UserId = userId };
        patientRepo.Upsert(patient);

        // Text classified as Prescription (contains "Rp.:")
        var preview = "Rp.: 1. Aspenter 75 mg, dimineata.\n2. Betaloc ZOK 50mg, seara.";

        await svc.AppendAsync(userId, Guid.NewGuid(), preview);

        Assert.Equal(2, medSvc.Added.Count);
        Assert.All(medSvc.Added, a => Assert.Equal(AddedByRole.OcrScan, a.Role));
        Assert.Contains(medSvc.Added, a => a.Dto.Name == "Aspenter");
        Assert.Contains(medSvc.Added, a => a.Dto.Name == "Betaloc ZOK");
    }

    [Fact]
    public async Task AutoAppend_LabResult_DoesNotAutoAddMedications()
    {
        var (svc, patientRepo, historyRepo, medSvc) = CreateAutoAppendService();

        var userId = Guid.NewGuid();
        var patient = new Patient { Id = Guid.NewGuid(), UserId = userId };
        patientRepo.Upsert(patient);

        // Text classified as LabResult — no "Rp.:" present
        var preview = "BULETIN DE ANALIZE\n1. Hemoglobina 14 g/dl";

        await svc.AppendAsync(userId, Guid.NewGuid(), preview);

        // No medications auto-added
        Assert.Empty(medSvc.Added);
    }

    [Fact]
    public async Task AutoAppend_Referral_DoesNotAutoAddMedications()
    {
        var (svc, patientRepo, _, medSvc) = CreateAutoAppendService();

        var userId = Guid.NewGuid();
        var patient = new Patient { Id = Guid.NewGuid(), UserId = userId };
        patientRepo.Upsert(patient);

        var preview = "BILET DE TRIMITERE\nMotivul trimiterii: control periodic";

        await svc.AppendAsync(userId, Guid.NewGuid(), preview);

        Assert.Empty(medSvc.Added);
    }

    private static (MedicalHistoryAutoAppendService svc, FakePatientRepository patientRepo, FakeHistoryRepository historyRepo, FakeMedicationService medSvc) CreateAutoAppendService()
    {
        var patientRepo = new FakePatientRepository();
        var historyRepo = new FakeHistoryRepository();
        var extractor = new MedicalHistoryExtractionService();
        var medSvc = new FakeMedicationService();
        var svc = new MedicalHistoryAutoAppendService(
            patientRepo,
            historyRepo,
            extractor,
            medSvc,
            NullLogger<MedicalHistoryAutoAppendService>.Instance);
        return (svc, patientRepo, historyRepo, medSvc);
    }

    private sealed class FakePatientRepository : IPatientRepository
    {
        private readonly Dictionary<Guid, Patient> _byUser = [];

        public void Upsert(Patient patient) => _byUser[patient.UserId] = patient;

        public Task<Patient?> GetByIdAsync(Guid id) => Task.FromResult(_byUser.Values.FirstOrDefault(x => x.Id == id));
        public Task<Patient?> GetByUserIdAsync(Guid userId) => Task.FromResult(_byUser.GetValueOrDefault(userId));
        public Task<IEnumerable<Patient>> GetAllAsync() => Task.FromResult<IEnumerable<Patient>>(_byUser.Values.ToList());
        public Task AddAsync(Patient patient) { Upsert(patient); return Task.CompletedTask; }
        public Task UpdateAsync(Patient patient) { Upsert(patient); return Task.CompletedTask; }
        public Task<IEnumerable<Patient>> GetDirtyAsync() => Task.FromResult<IEnumerable<Patient>>([]);
        public Task MarkSyncedAsync(IEnumerable<Patient> items) => Task.CompletedTask;
        public Task PurgeSyncedOlderThanAsync(DateTime cutoffUtc) => Task.CompletedTask;
        public Task<bool> ExistsAsync(Patient patient) => Task.FromResult(_byUser.ContainsKey(patient.UserId));
    }

    private sealed class FakeHistoryRepository : IMedicalHistoryEntryRepository
    {
        private readonly Dictionary<Guid, List<MedicalHistoryEntry>> _bySource = [];
        private readonly List<MedicalHistoryEntry> _all = [];

        public Task<IEnumerable<MedicalHistoryEntry>> GetByPatientAsync(Guid patientId)
            => Task.FromResult<IEnumerable<MedicalHistoryEntry>>(_all.Where(x => x.PatientId == patientId).ToList());

        public Task<IEnumerable<MedicalHistoryEntry>> GetBySourceDocumentAsync(Guid sourceDocumentId)
            => Task.FromResult<IEnumerable<MedicalHistoryEntry>>(_bySource.GetValueOrDefault(sourceDocumentId) ?? []);

        public Task<IEnumerable<MedicalHistoryEntry>> GetDirtyAsync()
            => Task.FromResult<IEnumerable<MedicalHistoryEntry>>(_all.Where(x => x.IsDirty).ToList());

        public Task AddRangeAsync(IEnumerable<MedicalHistoryEntry> entries)
        {
            foreach (var e in entries)
            {
                _all.Add(e);
                if (!_bySource.TryGetValue(e.SourceDocumentId, out var list))
                {
                    list = [];
                    _bySource[e.SourceDocumentId] = list;
                }
                list.Add(e);
            }
            return Task.CompletedTask;
        }

        public Task UpsertRangeAsync(IEnumerable<MedicalHistoryEntry> entries) => AddRangeAsync(entries);
        public Task MarkSyncedAsync(IEnumerable<Guid> ids) => Task.CompletedTask;
        public Task PurgeSyncedOlderThanAsync(DateTime cutoffUtc) => Task.CompletedTask;
    }

    private sealed class FakeMedicationService : IMedicationApplicationService
    {
        public List<(Guid PatientId, AddMedicationDto Dto, AddedByRole Role)> Added { get; } = [];

        public Task<MedicationDto> AddMedicationAsync(Guid patientId, AddMedicationDto dto, AddedByRole addedBy, bool skipInteractionCheck = false)
        {
            Added.Add((patientId, dto, addedBy));
            return Task.FromResult(new MedicationDto { Id = Guid.NewGuid(), Name = dto.Name, Dosage = dto.Dosage, AddedByRole = addedBy });
        }

        public Task<IEnumerable<MedicationInteractionDto>> CheckInteractionsAsync(IEnumerable<string> rxCuis)
            => Task.FromResult<IEnumerable<MedicationInteractionDto>>([]);
        public Task<bool> HasHighRiskInteractionsAsync(IEnumerable<string> rxCuis)
            => Task.FromResult(false);
        public Task<IEnumerable<DrugSearchResultDto>> SearchDrugsByNameAsync(string query, int maxResults = 8, CancellationToken ct = default)
            => Task.FromResult<IEnumerable<DrugSearchResultDto>>([]);
        public Task<MedicationListCache> GetMyMedicationsAsync(Guid patientId, bool forceRefresh = false)
            => Task.FromResult(new MedicationListCache());
        public Task DeleteMedicationAsync(Guid patientId, Guid medicationId) => Task.CompletedTask;
        public Task DiscontinueMedicationAsync(Guid patientId, Guid medicationId, string? reason = null) => Task.CompletedTask;
    }
}

