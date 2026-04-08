# Drainer Refactor Plan: Cloud Repositories → WebAPI HTTP Clients

## Overview

This document outlines the exact steps to refactor each drainer class to use HTTP API calls instead of direct cloud repository access, implementing Phase 3 of the migration plan.

## Current Architecture

All drainers inherit from `SyncDrainerBase<TModel>` and currently depend on:
- Local repository (e.g., `IUserRepository _local`)
- Cloud repository (e.g., `IUserRepository? _cloud`) - **TO BE REPLACED**
- Identity resolver for ID mapping between local and cloud

## Target Architecture

Replace cloud repositories with HTTP API clients:
- Local repository (unchanged)
- HTTP sync API client (e.g., `IUserSyncApiClient`)
- Identity resolver (may be simplified with stored cloud IDs)

## Step 1: Create HTTP Sync API Client Interfaces

Create a new project `DigitalTwin.SyncClient` with interfaces for each entity:

### IUserSyncApiClient
```csharp
public interface IUserSyncApiClient
{
    Task<UpsertUserResponse> UpsertAsync(UpsertUserRequest request, CancellationToken ct = default);
    // Pull methods if needed for user sync
}
```

### IPatientSyncApiClient
```csharp
public interface IPatientSyncApiClient
{
    Task<UpsertPatientResponse> UpsertAsync(UpsertPatientRequest request, CancellationToken ct = default);
    Task<GetPatientProfileResponse> GetProfileAsync(CancellationToken ct = default);
}
```

### IVitalSignSyncApiClient
```csharp
public interface IVitalSignSyncApiClient
{
    Task<AppendVitalSignsResponse> AppendAsync(AppendVitalSignsRequest request, CancellationToken ct = default);
    Task<GetVitalSignsResponse> GetAsync(GetVitalSignsRequest request, CancellationToken ct = default);
}
```

## Step 2: Implement HTTP Clients

Create concrete implementations using `HttpClient`:

### UserSyncApiClient
```csharp
public class UserSyncApiClient : IUserSyncApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IAuthTokenProvider _tokenProvider; // For JWT tokens
    
    public async Task<UpsertUserResponse> UpsertAsync(UpsertUserRequest request, CancellationToken ct = default)
    {
        var token = await _tokenProvider.GetTokenAsync();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        
        var response = await _httpClient.PostAsJsonAsync("/api/mobile/sync/users/upsert", request, ct);
        return await response.Content.ReadFromJsonAsync<UpsertUserResponse>(cancellationToken: ct) 
               ?? throw new InvalidOperationException("Failed to deserialize response");
    }
}
```

## Step 3: Refactor Each Drainer Class

### 3.1 UserSyncDrainer Refactor

**Current constructor:**
```csharp
public UserSyncDrainer(
    IUserRepository local,
    IUserRepository? cloud,
    ILogger<UserSyncDrainer> logger)
```

**New constructor:**
```csharp
public UserSyncDrainer(
    IUserRepository local,
    IUserSyncApiClient syncApi,
    ILogger<UserSyncDrainer> logger)
```

**Current UpsertToCloudBatchAsync:**
```csharp
protected override async Task UpsertToCloudBatchAsync(List<User> cloudItems, CancellationToken ct)
{
    foreach (var user in cloudItems)
    {
        var existing = await _cloud!.GetByEmailAsync(user.Email);
        if (existing is not null)
        {
            // Update logic...
            await _cloud.UpdateAsync(existing);
        }
        else
        {
            await _cloud.AddAsync(user);
        }
    }
}
```

**New UpsertToCloudBatchAsync:**
```csharp
protected override async Task UpsertToCloudBatchAsync(List<User> cloudItems, CancellationToken ct)
{
    foreach (var user in cloudItems)
    {
        ct.ThrowIfCancellationRequested();
        
        var request = new UpsertUserRequest
        {
            DeviceId = await GetDeviceIdAsync(), // New helper method
            RequestId = Guid.NewGuid().ToString(),
            User = MapToUserSyncDto(user)
        };
        
        var response = await _syncApi.UpsertAsync(request, ct);
        if (!response.Success)
        {
            Logger.LogWarning("[{Table}] Failed to sync user {Email}: {Error}", 
                TableName, user.Email, response.ErrorMessage);
            throw new InvalidOperationException($"Sync failed: {response.ErrorMessage}");
        }
    }
}
```

### 3.2 PatientSyncDrainer Refactor

**Key changes:**
- Replace `_cloud` repository with `IPatientSyncApiClient`
- Update `UpsertToCloudBatchAsync` to call HTTP API
- Update pull logic to use `GetProfileAsync`
- Simplify ID resolution (may store cloud IDs locally)

**Current MapToCloudBatchAsync:**
```csharp
var cloudUserId = await _identityResolver.ResolveCloudUserIdAsync(patient.UserId, ct);
```

**New approach - store cloud IDs:**
```csharp
// Assume we add CloudUserId and CloudPatientId columns to local entities
var cloudUserId = patient.CloudUserId ?? await ResolveAndStoreCloudUserIdAsync(patient.UserId, ct);
```

### 3.3 VitalSignSyncDrainer Refactor

**Current UpsertToCloudBatchAsync:**
```csharp
protected override async Task UpsertToCloudBatchAsync(List<VitalSign> cloudItems, CancellationToken ct)
    => await _cloud!.AddRangeAsync(cloudItems);
```

**New UpsertToCloudBatchAsync:**
```csharp
protected override async Task UpsertToCloudBatchAsync(List<VitalSign> cloudItems, CancellationToken ct)
{
    // Group by patient for batching
    var groupedByPatient = cloudItems.GroupBy(v => v.PatientId);
    
    foreach (var group in groupedByPatient)
    {
        ct.ThrowIfCancellationRequested();
        
        var request = new AppendVitalSignsRequest
        {
            DeviceId = await GetDeviceIdAsync(),
            RequestId = Guid.NewGuid().ToString(),
            PatientCloudId = group.Key,
            Items = group.Select(MapToVitalSignSyncDto).ToList()
        };
        
        var response = await _syncApi.AppendAsync(request, ct);
        if (response.AcceptedCount + response.DedupedCount != request.Items.Count)
        {
            Logger.LogWarning("[{Table}] Partial sync for patient {PatientId}: {Accepted}/{Total} accepted", 
                TableName, group.Key, response.AcceptedCount, request.Items.Count);
        }
    }
}
```

## Step 4: Update Composition Registration

### Current (DigitalTwin.Composition/DependencyInjection.cs):
```csharp
services.AddScoped<ISyncDrainer>(sp => new UserSyncDrainer(
    sp.GetRequiredService<IUserRepository>(),
    sp.GetKeyedService<IUserRepository>(Cloud),
    sp.GetRequiredService<ILogger<UserSyncDrainer>>()));
```

### New:
```csharp
services.AddScoped<ISyncDrainer>(sp => new UserSyncDrainer(
    sp.GetRequiredService<IUserRepository>(),
    sp.GetRequiredService<IUserSyncApiClient>(),
    sp.GetRequiredService<ILogger<UserSyncDrainer>>()));
```

### Register HTTP clients:
```csharp
services.AddHttpClient<IUserSyncApiClient, UserSyncApiClient>(client =>
{
    client.BaseAddress = new Uri(baseApiUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});
```

## Step 5: Update HealthDataSyncService

### Current TryWriteToCloudAsync:
```csharp
var cloud = scope.ServiceProvider.GetKeyedService<IVitalSignRepository>("Cloud");
await cloud.AddRangeAsync(cloudBatch);
```

### New TryWriteToCloudAsync:
```csharp
var syncApi = scope.ServiceProvider.GetRequiredService<IVitalSignSyncApiClient>();
var request = new AppendVitalSignsRequest
{
    DeviceId = _deviceId,
    RequestId = Guid.NewGuid().ToString(),
    PatientCloudId = cloudP.Id,
    Items = batch.Select(MapToVitalSignSyncDto).ToList()
};

var response = await syncApi.AppendAsync(request, ct);
return response.AcceptedCount > 0;
```

## Step 6: Handle Authentication

### Create IAuthTokenProvider:
```csharp
public interface IAuthTokenProvider
{
    Task<string> GetTokenAsync(CancellationToken ct = default);
    Task RefreshTokenAsync(CancellationToken ct = default);
}
```

### Implementation considerations:
- Store JWT token securely on device
- Handle token refresh when expired
- Integrate with existing auth flow

## Step 7: Error Handling and Retry Logic

### HTTP-specific error handling:
```csharp
try
{
    var response = await _syncApi.UpsertAsync(request, ct);
    // Handle success
}
catch (HttpRequestException ex) when (ex.Message.Contains("timeout"))
{
    // Network timeout - mark for retry
    throw new TransientSyncException("Network timeout", ex);
}
catch (HttpRequestException ex) when (ex.Message.Contains("401"))
{
    // Auth failure - refresh token and retry
    await _tokenProvider.RefreshTokenAsync(ct);
    throw new AuthenticationSyncException("Token expired", ex);
}
```

## Step 8: Testing Strategy

### Unit tests for each refactored drainer:
1. Mock `IXSyncApiClient` interfaces
2. Test successful sync scenarios
3. Test error handling (network, auth, server errors)
4. Test idempotency via requestId

### Integration tests:
1. Test against real WebAPI endpoints
2. Test offline → online sync scenarios
3. Test large batch handling
4. Test concurrent sync operations

## Step 9: Migration Checklist

- [ ] Create `DigitalTwin.SyncClient` project
- [ ] Implement all HTTP client interfaces
- [ ] Refactor `UserSyncDrainer`
- [ ] Refactor `PatientSyncDrainer`  
- [ ] Refactor `VitalSignSyncDrainer`
- [ ] Refactor remaining drainers (Medications, Sleep, etc.)
- [ ] Update `HealthDataSyncService`
- [ ] Update composition registration
- [ ] Implement authentication provider
- [ ] Add comprehensive error handling
- [ ] Remove all cloud repository dependencies from device build
- [ ] Verify no Npgsql references remain in embedded engine
- [ ] Test offline → online sync flows
- [ ] Performance test large sync batches

## Implementation Order

1. **UserSyncDrainer** - Simplest, no complex ID mapping
2. **PatientSyncDrainer** - Adds ID resolution complexity  
3. **VitalSignSyncDrainer** - Adds time-series and batching complexity
4. **Remaining drainers** - Apply same patterns
5. **HealthDataSyncService** - Update direct cloud writes
6. **Integration testing** - End-to-end validation

This approach maintains the existing drainer orchestration and Template Method pattern while replacing only the cloud access mechanism.