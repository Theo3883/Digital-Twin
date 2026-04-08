using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using DigitalTwin.Mobile.Application.DTOs;

namespace DigitalTwin.Mobile.Engine;

/// <summary>
/// Native C bridge for SwiftUI to call into the .NET Mobile Engine.
/// Provides C-compatible exports that can be called from Swift.
/// </summary>
public static class NativeBridge
{
    private static MobileEngine? _engine;
    private static ILogger? _logger;

    // ═══════════════════════════════════════════════════════════════════════════
    //  Lifecycle Management
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Initialize the mobile engine with database and API configuration
    /// </summary>
    // Internal methods (callable from the NativeAOT host)
    internal static IntPtr Initialize_Impl(IntPtr databasePathPtr, IntPtr apiBaseUrlPtr)
    {
        try
        {
            var databasePath = Marshal.PtrToStringUTF8(databasePathPtr) ?? throw new ArgumentNullException(nameof(databasePathPtr));
            var apiBaseUrl = Marshal.PtrToStringUTF8(apiBaseUrlPtr) ?? throw new ArgumentNullException(nameof(apiBaseUrlPtr));

            _engine = new MobileEngine(databasePath, apiBaseUrl);
            
            // Logger is optional here; don't reflect into engine internals.
            // Using a non-generic ILogger avoids the "static type as type argument" issue.
            _logger = null;

            _logger?.LogInformation("[NativeBridge] Engine initialized successfully");
            
            return AllocateString(JsonSerializer.Serialize(new OperationResultDto { Success = true }, MobileJsonContext.Default.OperationResultDto));
        }
        catch (Exception ex)
        {
            return AllocateString(JsonSerializer.Serialize(new OperationResultDto { Success = false, Error = $"Initialization failed: {ex.Message}" }, MobileJsonContext.Default.OperationResultDto));
        }
    }

    // Exported C ABI (kept for compatibility, but for the NativeHost path we
    // call the _Impl methods to avoid UnmanagedCallersOnly direct-call rules).
    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_initialize")]
    public static IntPtr Initialize(IntPtr databasePathPtr, IntPtr apiBaseUrlPtr)
        => Initialize_Impl(databasePathPtr, apiBaseUrlPtr);

    /// <summary>
    /// Initialize the database (call once on app startup)
    /// </summary>
    internal static IntPtr InitializeDatabase_Impl()
    {
        return ExecuteAsync(async () =>
        {
            if (_engine == null) throw new InvalidOperationException("Engine not initialized");
            return await _engine.InitializeDatabaseAsync();
        });
    }

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_initialize_database")]
    public static IntPtr InitializeDatabase()
        => InitializeDatabase_Impl();

    /// <summary>
    /// Dispose the engine and clean up resources
    /// </summary>
    internal static void Dispose_Impl()
    {
        try
        {
            _engine?.Dispose();
            _engine = null;
            _logger?.LogInformation("[NativeBridge] Engine disposed");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[NativeBridge] Error during disposal");
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_dispose")]
    public static void Dispose()
        => Dispose_Impl();

    // ═══════════════════════════════════════════════════════════════════════════
    //  Authentication
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Authenticate user with Google ID token
    /// </summary>
    internal static IntPtr Authenticate_Impl(IntPtr googleIdTokenPtr)
    {
        return ExecuteAsync(async () =>
        {
            if (_engine == null) throw new InvalidOperationException("Engine not initialized");
            
            var googleIdToken = Marshal.PtrToStringUTF8(googleIdTokenPtr) ?? throw new ArgumentNullException(nameof(googleIdTokenPtr));
            return await _engine.AuthenticateAsync(googleIdToken);
        });
    }

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_authenticate")]
    public static IntPtr Authenticate(IntPtr googleIdTokenPtr)
        => Authenticate_Impl(googleIdTokenPtr);

    /// <summary>
    /// Get current authenticated user
    /// </summary>
    internal static IntPtr GetCurrentUser_Impl()
    {
        return ExecuteAsync(async () =>
        {
            if (_engine == null) throw new InvalidOperationException("Engine not initialized");
            return await _engine.GetCurrentUserAsync();
        });
    }

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_get_current_user")]
    public static IntPtr GetCurrentUser()
        => GetCurrentUser_Impl();

    // ═══════════════════════════════════════════════════════════════════════════
    //  Patient Profile
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Get current patient profile
    /// </summary>
    internal static IntPtr GetPatientProfile_Impl()
    {
        return ExecuteAsync(async () =>
        {
            if (_engine == null) throw new InvalidOperationException("Engine not initialized");
            return await _engine.GetPatientProfileAsync();
        });
    }

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_get_patient_profile")]
    public static IntPtr GetPatientProfile()
        => GetPatientProfile_Impl();

    /// <summary>
    /// Update patient profile
    /// </summary>
    internal static IntPtr UpdatePatientProfile_Impl(IntPtr updateJsonPtr)
    {
        return ExecuteAsync(async () =>
        {
            if (_engine == null) throw new InvalidOperationException("Engine not initialized");
            
            var updateJson = Marshal.PtrToStringUTF8(updateJsonPtr) ?? throw new ArgumentNullException(nameof(updateJsonPtr));
            return await _engine.UpdatePatientProfileAsync(updateJson);
        });
    }

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_update_patient_profile")]
    public static IntPtr UpdatePatientProfile(IntPtr updateJsonPtr)
        => UpdatePatientProfile_Impl(updateJsonPtr);

    // ═══════════════════════════════════════════════════════════════════════════
    //  Vital Signs
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Record a single vital sign
    /// </summary>
    internal static IntPtr RecordVitalSign_Impl(IntPtr vitalSignJsonPtr)
    {
        return ExecuteAsync(async () =>
        {
            if (_engine == null) throw new InvalidOperationException("Engine not initialized");
            
            var vitalSignJson = Marshal.PtrToStringUTF8(vitalSignJsonPtr) ?? throw new ArgumentNullException(nameof(vitalSignJsonPtr));
            return await _engine.RecordVitalSignAsync(vitalSignJson);
        });
    }

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_record_vital_sign")]
    public static IntPtr RecordVitalSign(IntPtr vitalSignJsonPtr)
        => RecordVitalSign_Impl(vitalSignJsonPtr);

    /// <summary>
    /// Record multiple vital signs
    /// </summary>
    internal static IntPtr RecordVitalSigns_Impl(IntPtr vitalSignsJsonPtr)
    {
        return ExecuteAsync(async () =>
        {
            if (_engine == null) throw new InvalidOperationException("Engine not initialized");
            
            var vitalSignsJson = Marshal.PtrToStringUTF8(vitalSignsJsonPtr) ?? throw new ArgumentNullException(nameof(vitalSignsJsonPtr));
            return await _engine.RecordVitalSignsAsync(vitalSignsJson);
        });
    }

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_record_vital_signs")]
    public static IntPtr RecordVitalSigns(IntPtr vitalSignsJsonPtr)
        => RecordVitalSigns_Impl(vitalSignsJsonPtr);

    /// <summary>
    /// Get vital signs for date range
    /// </summary>
    internal static IntPtr GetVitalSigns_Impl(IntPtr fromDateIsoPtr, IntPtr toDateIsoPtr)
    {
        return ExecuteAsync(async () =>
        {
            if (_engine == null) throw new InvalidOperationException("Engine not initialized");
            
            var fromDateIso = Marshal.PtrToStringUTF8(fromDateIsoPtr);
            var toDateIso = Marshal.PtrToStringUTF8(toDateIsoPtr);
            
            return await _engine.GetVitalSignsAsync(fromDateIso, toDateIso);
        });
    }

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_get_vital_signs")]
    public static IntPtr GetVitalSigns(IntPtr fromDateIsoPtr, IntPtr toDateIsoPtr)
        => GetVitalSigns_Impl(fromDateIsoPtr, toDateIsoPtr);

    /// <summary>
    /// Get vital signs by type
    /// </summary>
    internal static IntPtr GetVitalSignsByType_Impl(int vitalTypeInt, IntPtr fromDateIsoPtr, IntPtr toDateIsoPtr)
    {
        return ExecuteAsync(async () =>
        {
            if (_engine == null) throw new InvalidOperationException("Engine not initialized");
            
            var fromDateIso = Marshal.PtrToStringUTF8(fromDateIsoPtr);
            var toDateIso = Marshal.PtrToStringUTF8(toDateIsoPtr);
            
            return await _engine.GetVitalSignsByTypeAsync(vitalTypeInt, fromDateIso, toDateIso);
        });
    }

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_get_vital_signs_by_type")]
    public static IntPtr GetVitalSignsByType(int vitalTypeInt, IntPtr fromDateIsoPtr, IntPtr toDateIsoPtr)
        => GetVitalSignsByType_Impl(vitalTypeInt, fromDateIsoPtr, toDateIsoPtr);

    // ═══════════════════════════════════════════════════════════════════════════
    //  Synchronization
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Perform full bidirectional sync
    /// </summary>
    internal static IntPtr PerformSync_Impl()
    {
        return ExecuteAsync(async () =>
        {
            if (_engine == null) throw new InvalidOperationException("Engine not initialized");
            return await _engine.PerformSyncAsync();
        });
    }

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_perform_sync")]
    public static IntPtr PerformSync()
        => PerformSync_Impl();

    /// <summary>
    /// Push local changes to cloud
    /// </summary>
    internal static IntPtr PushLocalChanges_Impl()
    {
        return ExecuteAsync(async () =>
        {
            if (_engine == null) throw new InvalidOperationException("Engine not initialized");
            return await _engine.PushLocalChangesAsync();
        });
    }

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_push_local_changes")]
    public static IntPtr PushLocalChanges()
        => PushLocalChanges_Impl();

    // ═══════════════════════════════════════════════════════════════════════════
    //  Memory Management
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Free memory allocated by the engine (call from Swift after using result)
    /// </summary>
    internal static void FreeString_Impl(IntPtr ptr)
    {
        if (ptr != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_free_string")]
    public static void FreeString(IntPtr ptr)
        => FreeString_Impl(ptr);

    // ═══════════════════════════════════════════════════════════════════════════
    //  Private Helpers
    // ═══════════════════════════════════════════════════════════════════════════

    private static IntPtr ExecuteAsync(Func<Task<string>> asyncOperation)
    {
        try
        {
            // Run async operation synchronously (bridge methods must be synchronous)
            var task = Task.Run(asyncOperation);
            task.Wait();
            
            var result = task.Result;
            return AllocateString(result);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[NativeBridge] Operation failed");
            return AllocateString(JsonSerializer.Serialize(new OperationResultDto { Success = false, Error = $"Operation failed: {ex.Message}" }, MobileJsonContext.Default.OperationResultDto));
        }
    }

    public sealed record OperationResultDto
    {
        public bool Success { get; init; }
        public string? Error { get; init; }
    }

    public sealed record RecordVitalSignsResultDto
    {
        public bool Success { get; init; }
        public int Count { get; init; }
        public string? Error { get; init; }
    }

    private static IntPtr AllocateString(string str)
    {
        if (string.IsNullOrEmpty(str))
            return IntPtr.Zero;

        var bytes = System.Text.Encoding.UTF8.GetBytes(str);
        var ptr = Marshal.AllocHGlobal(bytes.Length + 1);
        Marshal.Copy(bytes, 0, ptr, bytes.Length);
        Marshal.WriteByte(ptr, bytes.Length, 0); // Null terminator
        return ptr;
    }
}