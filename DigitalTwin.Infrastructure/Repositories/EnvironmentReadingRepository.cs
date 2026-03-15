using DigitalTwin.Domain.Enums;
using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Interfaces.Repositories;
using DigitalTwin.Domain.Models;
using DigitalTwin.Infrastructure.Data;
using DigitalTwin.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace DigitalTwin.Infrastructure.Repositories;

public class EnvironmentReadingRepository : IEnvironmentReadingRepository
{
    private readonly Func<HealthAppDbContext> _factory;
    private readonly bool _markDirtyOnInsert;

    public EnvironmentReadingRepository(Func<HealthAppDbContext> factory, bool markDirtyOnInsert = true)
    {
        _factory = factory;
        _markDirtyOnInsert = markDirtyOnInsert;
    }

    public async Task AddAsync(EnvironmentReading reading, bool markDirty = true)
    {
        var shouldMarkDirty = _markDirtyOnInsert && markDirty;
        await using var db = _factory();
        var entity = ToEntity(reading);
        entity.IsDirty = shouldMarkDirty;
        if (!shouldMarkDirty) entity.SyncedAt = DateTime.UtcNow;
        db.EnvironmentReadings.Add(entity);
        await db.SaveChangesAsync();
    }

    public async Task AddRangeAsync(IEnumerable<EnvironmentReading> readings, bool markDirty = true)
    {
        var shouldMarkDirty = _markDirtyOnInsert && markDirty;
        var entities = readings.Select(ToEntity).ToList();
        if (entities.Count == 0) return;
        foreach (var e in entities) e.IsDirty = shouldMarkDirty;
        if (!shouldMarkDirty) foreach (var e in entities) e.SyncedAt = DateTime.UtcNow;
        await using var db = _factory();
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            await db.EnvironmentReadings.AddRangeAsync(entities);
            await db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<IEnumerable<EnvironmentReading>> GetDirtyAsync()
    {
        await using var db = _factory();
        var entities = await db.EnvironmentReadings
            .Where(r => r.IsDirty)
            .OrderBy(r => r.Timestamp)
            .ToListAsync();
        return entities.Select(ToDomain);
    }

    public async Task<IEnumerable<EnvironmentReading>> GetSinceAsync(DateTime since, int limit = 200)
    {
        await using var db = _factory();
        var entities = await db.EnvironmentReadings
            .Where(r => r.Timestamp >= since)
            .OrderByDescending(r => r.Timestamp)
            .Take(limit)
            .ToListAsync();
        return entities.Select(ToDomain);
    }

    public async Task<bool> ExistsAsync(DateTime timestamp)
    {
        await using var db = _factory();
        return await db.EnvironmentReadings.AnyAsync(r => r.Timestamp == timestamp);
    }

    public async Task MarkSyncedAsync(DateTime beforeOrAtTimestamp)
    {
        await using var db = _factory();
        await db.EnvironmentReadings
            .Where(r => r.IsDirty && r.Timestamp <= beforeOrAtTimestamp)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.IsDirty, false)
                .SetProperty(r => r.SyncedAt, DateTime.UtcNow));
    }

    public async Task PurgeSyncedOlderThanAsync(DateTime cutoffUtc)
    {
        await using var db = _factory();
        await db.EnvironmentReadings
            .Where(r => !r.IsDirty && r.SyncedAt.HasValue && r.SyncedAt.Value < cutoffUtc)
            .ExecuteDeleteAsync();
    }

    private static EnvironmentReading ToDomain(EnvironmentReadingEntity e) => new()
    {
        Latitude = (double)e.Latitude,
        Longitude = (double)e.Longitude,
        PM25 = (double)e.PM25,
        PM10 = (double)e.PM10,
        O3 = (double)e.O3,
        NO2 = (double)e.NO2,
        Temperature = (double)e.Temperature,
        Humidity = (double)e.Humidity,
        AirQuality = (AirQualityLevel)e.AirQualityLevel,
        AqiIndex = e.AqiIndex,
        Timestamp = e.Timestamp
    };

    private static EnvironmentReadingEntity ToEntity(EnvironmentReading r) => new()
    {
        Latitude = (decimal)r.Latitude,
        Longitude = (decimal)r.Longitude,
        PM25 = (decimal)r.PM25,
        PM10 = (decimal)r.PM10,
        O3 = (decimal)r.O3,
        NO2 = (decimal)r.NO2,
        Temperature = (decimal)r.Temperature,
        Humidity = (decimal)r.Humidity,
        AirQualityLevel = (int)r.AirQuality,
        AqiIndex = r.AqiIndex,
        Timestamp = r.Timestamp,
    };
}
