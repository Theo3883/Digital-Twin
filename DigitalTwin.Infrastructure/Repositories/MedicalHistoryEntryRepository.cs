using DigitalTwin.Domain.Interfaces.Repositories;
using DigitalTwin.Domain.Models;
using DigitalTwin.Infrastructure.Data;
using DigitalTwin.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace DigitalTwin.Infrastructure.Repositories;

public sealed class MedicalHistoryEntryRepository : IMedicalHistoryEntryRepository
{
    private readonly Func<HealthAppDbContext> _factory;
    private readonly bool _markDirtyOnInsert;

    public MedicalHistoryEntryRepository(Func<HealthAppDbContext> factory, bool markDirtyOnInsert = true)
    {
        _factory = factory;
        _markDirtyOnInsert = markDirtyOnInsert;
    }

    public async Task<IEnumerable<MedicalHistoryEntry>> GetByPatientAsync(Guid patientId)
    {
        await using var db = _factory();
        var list = await db.MedicalHistoryEntries
            .Where(x => x.PatientId == patientId && x.DeletedAt == null)
            .OrderByDescending(x => x.EventDate)
            .ToListAsync();
        return list.Select(Map).ToList();
    }

    public async Task<IEnumerable<MedicalHistoryEntry>> GetBySourceDocumentAsync(Guid sourceDocumentId)
    {
        await using var db = _factory();
        var list = await db.MedicalHistoryEntries
            .Where(x => x.SourceDocumentId == sourceDocumentId && x.DeletedAt == null)
            .ToListAsync();
        return list.Select(Map).ToList();
    }

    public async Task<IEnumerable<MedicalHistoryEntry>> GetDirtyAsync()
    {
        await using var db = _factory();
        var list = await db.MedicalHistoryEntries.Where(x => x.IsDirty && x.DeletedAt == null).ToListAsync();
        return list.Select(Map).ToList();
    }

    public async Task AddRangeAsync(IEnumerable<MedicalHistoryEntry> entries)
    {
        await using var db = _factory();
        var entities = entries.Select(e => ToEntity(e, _markDirtyOnInsert)).ToList();
        await db.MedicalHistoryEntries.AddRangeAsync(entities);
        await db.SaveChangesAsync();
    }

    public async Task UpsertRangeAsync(IEnumerable<MedicalHistoryEntry> entries)
    {
        await using var db = _factory();
        foreach (var entry in entries)
        {
            var existing = await db.MedicalHistoryEntries.FirstOrDefaultAsync(x => x.Id == entry.Id);
            if (existing is null)
            {
                db.MedicalHistoryEntries.Add(ToEntity(entry, markDirty: false));
                continue;
            }

            existing.Title = entry.Title;
            existing.MedicationName = entry.MedicationName;
            existing.Dosage = entry.Dosage;
            existing.Frequency = entry.Frequency;
            existing.Duration = entry.Duration;
            existing.Notes = entry.Notes;
            existing.Summary = entry.Summary;
            existing.Confidence = entry.Confidence;
            existing.EventDate = entry.EventDate;
            existing.UpdatedAt = entry.UpdatedAt;
            existing.IsDirty = false;
            existing.SyncedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync();
    }

    public async Task MarkSyncedAsync(IEnumerable<Guid> ids)
    {
        var set = ids.ToHashSet();
        await using var db = _factory();
        await db.MedicalHistoryEntries
            .Where(x => set.Contains(x.Id))
            .ExecuteUpdateAsync(u => u
                .SetProperty(x => x.IsDirty, false)
                .SetProperty(x => x.SyncedAt, DateTime.UtcNow));
    }

    public async Task PurgeSyncedOlderThanAsync(DateTime cutoffUtc)
    {
        await using var db = _factory();
        await db.MedicalHistoryEntries
            .Where(x => !x.IsDirty && x.DeletedAt != null && x.SyncedAt != null && x.SyncedAt < cutoffUtc)
            .ExecuteDeleteAsync();
    }

    private static MedicalHistoryEntry Map(MedicalHistoryEntryEntity e) => new()
    {
        Id = e.Id,
        PatientId = e.PatientId,
        SourceDocumentId = e.SourceDocumentId,
        Title = e.Title,
        MedicationName = e.MedicationName,
        Dosage = e.Dosage,
        Frequency = e.Frequency,
        Duration = e.Duration,
        Notes = e.Notes,
        Summary = e.Summary,
        Confidence = e.Confidence,
        EventDate = e.EventDate,
        CreatedAt = e.CreatedAt,
        UpdatedAt = e.UpdatedAt,
        IsDirty = e.IsDirty,
        SyncedAt = e.SyncedAt,
        DeletedAt = e.DeletedAt
    };

    private static MedicalHistoryEntryEntity ToEntity(MedicalHistoryEntry e, bool markDirty) => new()
    {
        Id = e.Id,
        PatientId = e.PatientId,
        SourceDocumentId = e.SourceDocumentId,
        Title = e.Title,
        MedicationName = e.MedicationName,
        Dosage = e.Dosage,
        Frequency = e.Frequency,
        Duration = e.Duration,
        Notes = e.Notes,
        Summary = e.Summary,
        Confidence = e.Confidence,
        EventDate = e.EventDate,
        CreatedAt = e.CreatedAt,
        UpdatedAt = e.UpdatedAt,
        IsDirty = markDirty,
        SyncedAt = e.SyncedAt,
        DeletedAt = e.DeletedAt
    };
}

