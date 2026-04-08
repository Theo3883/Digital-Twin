namespace DigitalTwin.Application.DTOs;

/// <summary>
/// Cached medication list + auto interaction banner data (preferences JSON, not the medical DB).
/// </summary>
public sealed class MedicationListCache
{
    public Guid PatientId { get; init; }

    public DateTime CachedAtUtc { get; init; }

    public List<MedicationDto> Medications { get; init; } = [];

    public List<MedicationInteractionDto> AutoInteractions { get; init; } = [];
}

/// <summary>
/// Key and TTL for <see cref="MedicationListCache"/> in <see cref="Interfaces.IPreferencesJsonCache"/>.
/// </summary>
public static class MedicationListCachePreferences
{
    public const string Key = "med_list_snapshot_v1";

    public static readonly TimeSpan Ttl = TimeSpan.FromHours(24);
}
