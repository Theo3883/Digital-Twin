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
    public async Task AutoAppend_IsIdempotent_BySourceDocumentId()
    {
        var patientRepo = new FakePatientRepository();
        var historyRepo = new FakeHistoryRepository();
        var extractor = new MedicalHistoryExtractionService();
        var svc = new MedicalHistoryAutoAppendService(
            patientRepo,
            historyRepo,
            extractor,
            NullLogger<MedicalHistoryAutoAppendService>.Instance);

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
}

