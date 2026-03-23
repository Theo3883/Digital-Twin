using DigitalTwin.Domain.Interfaces.Repositories;
using DigitalTwin.Domain.Models;
using DigitalTwin.OCR.Models;
using DigitalTwin.OCR.Policies;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.OCR.Services;

/// <summary>
/// Persists OCR results to the local SQLite database for later cloud sync.
/// Only sanitized metadata is saved — raw OCR text and vault paths stay off-cloud.
/// </summary>
public sealed class OcrSyncPreparationService
{
    private readonly IOcrDocumentRepository _repository;
    private readonly ILogger<OcrSyncPreparationService> _logger;

    public OcrSyncPreparationService(
        IOcrDocumentRepository repository,
        ILogger<OcrSyncPreparationService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<OcrResult<bool>> SaveAsync(
        OcrDocumentSyncRecord record,
        CancellationToken ct = default)
    {
        try
        {
            var domain = new OcrDocument
            {
                Id = record.Id,
                PatientId = record.PatientId,
                OpaqueInternalName = record.OpaqueInternalName,
                MimeType = record.MimeType,
                PageCount = record.PageCount,
                Sha256OfNormalized = record.Sha256OfNormalized,
                SanitizedOcrPreview = record.SanitizedOcrPreview,
                EncryptedVaultPath = record.EncryptedVaultPath,
                ScannedAt = record.ScannedAt,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsDirty = true
            };

            await _repository.AddAsync(domain);

            _logger.LogInformation("[OCR Sync] Saved {Ref} to local store.",
                LoggingRedactionPolicy.SafeDocumentRef(record.Id));

            return OcrResult<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OCR Sync] SaveAsync failed for {Ref}.",
                LoggingRedactionPolicy.SafeDocumentRef(record.Id));
            return OcrResult<bool>.Fail("Failed to save OCR sync record.");
        }
    }

    public async Task<OcrResult<bool>> DeleteAsync(Guid documentId, CancellationToken ct = default)
    {
        try
        {
            await _repository.DeleteAsync(documentId);
            return OcrResult<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OCR Sync] DeleteAsync failed.");
            return OcrResult<bool>.Fail("Failed to delete OCR record.");
        }
    }

    public async Task<IReadOnlyList<OcrDocumentSyncRecord>> GetByPatientAsync(Guid patientId)
    {
        try
        {
            var docs = await _repository.GetByPatientAsync(patientId);
            return docs.Select(d => new OcrDocumentSyncRecord(
                d.Id,
                d.PatientId,
                d.OpaqueInternalName,
                d.MimeType,
                d.PageCount,
                d.Sha256OfNormalized,
                d.SanitizedOcrPreview,
                d.EncryptedVaultPath,
                d.ScannedAt)).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OCR Sync] GetByPatientAsync failed.");
            return [];
        }
    }
}
