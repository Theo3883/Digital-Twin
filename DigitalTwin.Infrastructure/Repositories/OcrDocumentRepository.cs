using DigitalTwin.Domain.Interfaces.Repositories;
using DigitalTwin.Domain.Models;
using DigitalTwin.Infrastructure.Data;
using DigitalTwin.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace DigitalTwin.Infrastructure.Repositories;

public sealed class OcrDocumentRepository : IOcrDocumentRepository
{
    private readonly Func<HealthAppDbContext> _contextFactory;
    private readonly bool _markDirtyOnInsert;

    public OcrDocumentRepository(Func<HealthAppDbContext> contextFactory, bool markDirtyOnInsert = true)
    {
        _contextFactory = contextFactory;
        _markDirtyOnInsert = markDirtyOnInsert;
    }

    public async Task<IEnumerable<OcrDocument>> GetByPatientAsync(Guid patientId)
    {
        using var ctx = _contextFactory();
        var entities = await ctx.OcrDocuments
            .Where(d => d.PatientId == patientId && d.DeletedAt == null)
            .OrderByDescending(d => d.ScannedAt)
            .ToListAsync();
        return entities.Select(Map).ToList();
    }

    public async Task<OcrDocument?> GetByIdAsync(Guid id)
    {
        using var ctx = _contextFactory();
        var entity = await ctx.OcrDocuments.FirstOrDefaultAsync(d => d.Id == id && d.DeletedAt == null);
        return entity is null ? null : Map(entity);
    }

    public async Task<IEnumerable<OcrDocument>> GetDirtyAsync()
    {
        using var ctx = _contextFactory();
        var entities = await ctx.OcrDocuments
            .Where(d => d.IsDirty && d.DeletedAt == null)
            .ToListAsync();
        return entities.Select(Map).ToList();
    }

    public async Task AddAsync(OcrDocument document)
    {
        using var ctx = _contextFactory();
        ctx.OcrDocuments.Add(ToEntity(document, _markDirtyOnInsert));
        await ctx.SaveChangesAsync();
    }

    public async Task UpdateAsync(OcrDocument document)
    {
        using var ctx = _contextFactory();
        var entity = await ctx.OcrDocuments.FirstOrDefaultAsync(d => d.Id == document.Id);
        if (entity is null) return;

        entity.SanitizedOcrPreview = document.SanitizedOcrPreview;
        entity.EncryptedVaultPath = document.EncryptedVaultPath;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.IsDirty = _markDirtyOnInsert;

        await ctx.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        using var ctx = _contextFactory();
        var entity = await ctx.OcrDocuments.FirstOrDefaultAsync(d => d.Id == id);
        if (entity is null) return;

        entity.DeletedAt = DateTime.UtcNow;
        entity.IsDirty = true;
        await ctx.SaveChangesAsync();
    }

    public async Task MarkSyncedAsync(Guid id)
    {
        using var ctx = _contextFactory();
        var entity = await ctx.OcrDocuments.FirstOrDefaultAsync(d => d.Id == id);
        if (entity is null) return;

        entity.IsDirty = false;
        entity.SyncedAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync();
    }

    public async Task UpsertRangeAsync(IEnumerable<OcrDocument> documents)
    {
        using var ctx = _contextFactory();
        foreach (var doc in documents)
        {
            var existing = await ctx.OcrDocuments.FirstOrDefaultAsync(d => d.Id == doc.Id);
            if (existing is null)
            {
                ctx.OcrDocuments.Add(ToEntity(doc, markDirty: false));
            }
            else
            {
                existing.SanitizedOcrPreview = doc.SanitizedOcrPreview;
                existing.UpdatedAt = doc.UpdatedAt;
                existing.IsDirty = false;
                existing.SyncedAt = DateTime.UtcNow;
            }
        }
        await ctx.SaveChangesAsync();
    }

    public async Task PurgeSyncedOlderThanAsync(DateTime olderThan)
    {
        using var ctx = _contextFactory();
        var old = await ctx.OcrDocuments
            .Where(d => !d.IsDirty && d.SyncedAt != null && d.SyncedAt < olderThan && d.DeletedAt != null)
            .ToListAsync();
        ctx.OcrDocuments.RemoveRange(old);
        await ctx.SaveChangesAsync();
    }

    // ── Mapping helpers ───────────────────────────────────────────────────────

    private static OcrDocument Map(OcrDocumentEntity e) => new()
    {
        Id = e.Id,
        PatientId = e.PatientId,
        OpaqueInternalName = e.OpaqueInternalName,
        MimeType = e.MimeType,
        DocumentType = e.DocumentType,
        PageCount = e.PageCount,
        Sha256OfNormalized = e.Sha256OfNormalized,
        SanitizedOcrPreview = e.SanitizedOcrPreview,
        EncryptedVaultPath = e.EncryptedVaultPath,
        ScannedAt = e.ScannedAt,
        CreatedAt = e.CreatedAt,
        UpdatedAt = e.UpdatedAt,
        IsDirty = e.IsDirty,
        SyncedAt = e.SyncedAt,
        DeletedAt = e.DeletedAt
    };

    private static OcrDocumentEntity ToEntity(OcrDocument d, bool markDirty) => new()
    {
        Id = d.Id,
        PatientId = d.PatientId,
        OpaqueInternalName = d.OpaqueInternalName,
        MimeType = d.MimeType,
        DocumentType = d.DocumentType,
        PageCount = d.PageCount,
        Sha256OfNormalized = d.Sha256OfNormalized,
        SanitizedOcrPreview = d.SanitizedOcrPreview,
        EncryptedVaultPath = d.EncryptedVaultPath,
        ScannedAt = d.ScannedAt,
        CreatedAt = d.CreatedAt,
        UpdatedAt = d.UpdatedAt,
        IsDirty = markDirty,
        SyncedAt = d.SyncedAt,
        DeletedAt = d.DeletedAt
    };

}
