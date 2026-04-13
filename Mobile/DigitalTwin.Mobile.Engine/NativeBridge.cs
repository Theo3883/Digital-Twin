using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
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
    private static int _globalExceptionHandlersInstalled;

    static NativeBridge()
    {
        EnsureGlobalExceptionHandlers();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  Lifecycle Management
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Initialize the mobile engine with database and API configuration
    /// </summary>
    // Internal methods (callable from the NativeAOT host)
    internal static IntPtr Initialize_Impl(IntPtr databasePathPtr, IntPtr apiBaseUrlPtr, IntPtr geminiApiKeyPtr, IntPtr openWeatherApiKeyPtr, IntPtr googleOAuthClientIdPtr, IntPtr openRouterApiKeyPtr, IntPtr openRouterModelPtr)
    {
        try
        {
            EnsureGlobalExceptionHandlers();

            var databasePath = Marshal.PtrToStringUTF8(databasePathPtr) ?? throw new ArgumentNullException(nameof(databasePathPtr));
            var apiBaseUrl = Marshal.PtrToStringUTF8(apiBaseUrlPtr) ?? "";
            var geminiApiKey = Marshal.PtrToStringUTF8(geminiApiKeyPtr);
            var openWeatherApiKey = Marshal.PtrToStringUTF8(openWeatherApiKeyPtr);
            var googleOAuthClientId = Marshal.PtrToStringUTF8(googleOAuthClientIdPtr);
            var openRouterApiKey = Marshal.PtrToStringUTF8(openRouterApiKeyPtr);
            var openRouterModel = Marshal.PtrToStringUTF8(openRouterModelPtr);

            _engine = new MobileEngine(databasePath, apiBaseUrl, geminiApiKey, openWeatherApiKey, googleOAuthClientId, openRouterApiKey, openRouterModel);
            
            // Logger is optional here; don't reflect into engine internals.
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
    public static IntPtr Initialize(IntPtr databasePathPtr, IntPtr apiBaseUrlPtr, IntPtr geminiApiKeyPtr, IntPtr openWeatherApiKeyPtr, IntPtr googleOAuthClientIdPtr, IntPtr openRouterApiKeyPtr, IntPtr openRouterModelPtr)
        => Initialize_Impl(databasePathPtr, apiBaseUrlPtr, geminiApiKeyPtr, openWeatherApiKeyPtr, googleOAuthClientIdPtr, openRouterApiKeyPtr, openRouterModelPtr);

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

    /// <summary>
    /// Update current authenticated user profile
    /// </summary>
    internal static IntPtr UpdateCurrentUser_Impl(IntPtr updateJsonPtr)
    {
        return ExecuteAsync(async () =>
        {
            if (_engine == null) throw new InvalidOperationException("Engine not initialized");

            var updateJson = Marshal.PtrToStringUTF8(updateJsonPtr) ?? throw new ArgumentNullException(nameof(updateJsonPtr));
            return await _engine.UpdateCurrentUserAsync(updateJson);
        });
    }

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_update_current_user")]
    public static IntPtr UpdateCurrentUser(IntPtr updateJsonPtr)
        => UpdateCurrentUser_Impl(updateJsonPtr);

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
    //  Medications
    // ═══════════════════════════════════════════════════════════════════════════

    internal static IntPtr GetMedications_Impl()
    {
        return ExecuteAsync(async () =>
        {
            if (_engine == null) throw new InvalidOperationException("Engine not initialized");
            return await _engine.GetMedicationsAsync();
        });
    }

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_get_medications")]
    public static IntPtr GetMedications() => GetMedications_Impl();

    internal static IntPtr AddMedication_Impl(IntPtr inputJsonPtr)
    {
        return ExecuteAsync(async () =>
        {
            if (_engine == null) throw new InvalidOperationException("Engine not initialized");
            var json = Marshal.PtrToStringUTF8(inputJsonPtr) ?? throw new ArgumentNullException(nameof(inputJsonPtr));
            return await _engine.AddMedicationAsync(json);
        });
    }

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_add_medication")]
    public static IntPtr AddMedication(IntPtr inputJsonPtr) => AddMedication_Impl(inputJsonPtr);

    internal static IntPtr DiscontinueMedication_Impl(IntPtr inputJsonPtr)
    {
        return ExecuteAsync(async () =>
        {
            if (_engine == null) throw new InvalidOperationException("Engine not initialized");
            var json = Marshal.PtrToStringUTF8(inputJsonPtr) ?? throw new ArgumentNullException(nameof(inputJsonPtr));
            return await _engine.DiscontinueMedicationAsync(json);
        });
    }

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_discontinue_medication")]
    public static IntPtr DiscontinueMedication(IntPtr inputJsonPtr) => DiscontinueMedication_Impl(inputJsonPtr);

    internal static IntPtr SearchDrugs_Impl(IntPtr queryPtr)
    {
        return ExecuteAsync(async () =>
        {
            if (_engine == null) throw new InvalidOperationException("Engine not initialized");
            var query = Marshal.PtrToStringUTF8(queryPtr) ?? throw new ArgumentNullException(nameof(queryPtr));
            return await _engine.SearchDrugsAsync(query);
        });
    }

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_search_drugs")]
    public static IntPtr SearchDrugs(IntPtr queryPtr) => SearchDrugs_Impl(queryPtr);

    internal static IntPtr CheckInteractions_Impl(IntPtr rxCuisJsonPtr)
    {
        return ExecuteAsync(async () =>
        {
            if (_engine == null) throw new InvalidOperationException("Engine not initialized");
            var json = Marshal.PtrToStringUTF8(rxCuisJsonPtr) ?? throw new ArgumentNullException(nameof(rxCuisJsonPtr));
            return await _engine.CheckInteractionsAsync(json);
        });
    }

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_check_interactions")]
    public static IntPtr CheckInteractions(IntPtr rxCuisJsonPtr) => CheckInteractions_Impl(rxCuisJsonPtr);

    // ═══════════════════════════════════════════════════════════════════════════
    //  Environment
    // ═══════════════════════════════════════════════════════════════════════════

    internal static IntPtr GetEnvironmentReading_Impl(double latitude, double longitude)
    {
        return ExecuteAsync(async () =>
        {
            if (_engine == null) throw new InvalidOperationException("Engine not initialized");
            return await _engine.GetEnvironmentReadingAsync(latitude, longitude);
        });
    }

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_get_environment_reading")]
    public static IntPtr GetEnvironmentReading(double latitude, double longitude)
        => GetEnvironmentReading_Impl(latitude, longitude);

    internal static IntPtr GetLatestEnvironmentReading_Impl()
    {
        return ExecuteAsync(async () =>
        {
            if (_engine == null) throw new InvalidOperationException("Engine not initialized");
            return await _engine.GetLatestEnvironmentReadingAsync();
        });
    }

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_get_latest_environment_reading")]
    public static IntPtr GetLatestEnvironmentReading() => GetLatestEnvironmentReading_Impl();

    // ═══════════════════════════════════════════════════════════════════════════
    //  ECG Triage
    // ═══════════════════════════════════════════════════════════════════════════

    internal static IntPtr EvaluateEcgFrame_Impl(IntPtr frameJsonPtr)
    {
        try
        {
            if (_engine == null) throw new InvalidOperationException("Engine not initialized");
            var json = Marshal.PtrToStringUTF8(frameJsonPtr) ?? throw new ArgumentNullException(nameof(frameJsonPtr));
            var result = _engine.EvaluateEcgFrame(json);
            return AllocateString(result);
        }
        catch (Exception ex)
        {
            return AllocateString(System.Text.Json.JsonSerializer.Serialize(
                new OperationResultDto { Success = false, Error = ex.Message },
                MobileJsonContext.Default.OperationResultDto));
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_evaluate_ecg_frame")]
    public static IntPtr EvaluateEcgFrame(IntPtr frameJsonPtr) => EvaluateEcgFrame_Impl(frameJsonPtr);

    // ═══════════════════════════════════════════════════════════════════════════
    //  AI Chat
    // ═══════════════════════════════════════════════════════════════════════════

    internal static IntPtr SendChatMessage_Impl(IntPtr messagePtr)
    {
        return ExecuteAsync(async () =>
        {
            if (_engine == null) throw new InvalidOperationException("Engine not initialized");
            var message = Marshal.PtrToStringUTF8(messagePtr) ?? throw new ArgumentNullException(nameof(messagePtr));
            return await _engine.SendChatMessageAsync(message);
        });
    }

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_send_chat_message")]
    public static IntPtr SendChatMessage(IntPtr messagePtr) => SendChatMessage_Impl(messagePtr);

    internal static IntPtr GetChatHistory_Impl()
    {
        return ExecuteAsync(async () =>
        {
            if (_engine == null) throw new InvalidOperationException("Engine not initialized");
            return await _engine.GetChatHistoryAsync();
        });
    }

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_get_chat_history")]
    public static IntPtr GetChatHistory() => GetChatHistory_Impl();

    internal static IntPtr ClearChatHistory_Impl()
    {
        return ExecuteAsync(async () =>
        {
            if (_engine == null) throw new InvalidOperationException("Engine not initialized");
            return await _engine.ClearChatHistoryAsync();
        });
    }

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_clear_chat_history")]
    public static IntPtr ClearChatHistory() => ClearChatHistory_Impl();

    // ═══════════════════════════════════════════════════════════════════════════
    //  Coaching
    // ═══════════════════════════════════════════════════════════════════════════

    internal static IntPtr GetCoachingAdvice_Impl()
    {
        return ExecuteAsync(async () =>
        {
            if (_engine == null) throw new InvalidOperationException("Engine not initialized");
            return await _engine.GetCoachingAdviceAsync();
        });
    }

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_get_coaching_advice")]
    public static IntPtr GetCoachingAdvice() => GetCoachingAdvice_Impl();

    // ═══════════════════════════════════════════════════════════════════════════
    //  Sleep
    // ═══════════════════════════════════════════════════════════════════════════

    internal static IntPtr RecordSleepSession_Impl(IntPtr sessionJsonPtr)
    {
        return ExecuteAsync(async () =>
        {
            if (_engine == null) throw new InvalidOperationException("Engine not initialized");
            var json = Marshal.PtrToStringUTF8(sessionJsonPtr) ?? throw new ArgumentNullException(nameof(sessionJsonPtr));
            return await _engine.RecordSleepSessionAsync(json);
        });
    }

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_record_sleep_session")]
    public static IntPtr RecordSleepSession(IntPtr sessionJsonPtr) => RecordSleepSession_Impl(sessionJsonPtr);

    internal static IntPtr GetSleepSessions_Impl(IntPtr fromDateIsoPtr, IntPtr toDateIsoPtr)
    {
        return ExecuteAsync(async () =>
        {
            if (_engine == null) throw new InvalidOperationException("Engine not initialized");
            var from = Marshal.PtrToStringUTF8(fromDateIsoPtr);
            var to = Marshal.PtrToStringUTF8(toDateIsoPtr);
            return await _engine.GetSleepSessionsAsync(from, to);
        });
    }

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_get_sleep_sessions")]
    public static IntPtr GetSleepSessions(IntPtr fromDateIsoPtr, IntPtr toDateIsoPtr)
        => GetSleepSessions_Impl(fromDateIsoPtr, toDateIsoPtr);

    // ═══════════════════════════════════════════════════════════════════════════
    //  Medical History & OCR
    // ═══════════════════════════════════════════════════════════════════════════

    internal static IntPtr GetMedicalHistory_Impl()
    {
        return ExecuteAsync(async () =>
        {
            if (_engine == null) throw new InvalidOperationException("Engine not initialized");
            return await _engine.GetMedicalHistoryAsync();
        });
    }

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_get_medical_history")]
    public static IntPtr GetMedicalHistory() => GetMedicalHistory_Impl();

    internal static IntPtr GetOcrDocuments_Impl()
    {
        return ExecuteAsync(async () =>
        {
            if (_engine == null) throw new InvalidOperationException("Engine not initialized");
            return await _engine.GetOcrDocumentsAsync();
        });
    }

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_get_ocr_documents")]
    public static IntPtr GetOcrDocuments() => GetOcrDocuments_Impl();

    // ═══════════════════════════════════════════════════════════════════════════
    //  OCR Text Processing
    // ═══════════════════════════════════════════════════════════════════════════

    internal static IntPtr ClassifyDocument_Impl(IntPtr ocrTextPtr)
    {
        try
        {
            if (_engine == null) throw new InvalidOperationException("Engine not initialized");
            var ocrText = Marshal.PtrToStringUTF8(ocrTextPtr) ?? "";
            var result = _engine.ClassifyDocument(ocrText);
            return AllocateString(result);
        }
        catch
        {
            return AllocateString("Unknown");
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_classify_document")]
    public static IntPtr ClassifyDocument(IntPtr ocrTextPtr) => ClassifyDocument_Impl(ocrTextPtr);

    internal static IntPtr ExtractIdentity_Impl(IntPtr ocrTextPtr)
    {
        try
        {
            if (_engine == null) throw new InvalidOperationException("Engine not initialized");
            var ocrText = Marshal.PtrToStringUTF8(ocrTextPtr) ?? "";
            var result = _engine.ExtractIdentity(ocrText);
            return AllocateString(result);
        }
        catch (Exception ex)
        {
            return AllocateString(JsonSerializer.Serialize(
                new OperationResultDto { Success = false, Error = ex.Message },
                MobileJsonContext.Default.OperationResultDto));
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_extract_identity")]
    public static IntPtr ExtractIdentity(IntPtr ocrTextPtr) => ExtractIdentity_Impl(ocrTextPtr);

    internal static IntPtr ValidateIdentity_Impl(IntPtr ocrTextPtr)
    {
        return ExecuteAsync(async () =>
        {
            if (_engine == null) throw new InvalidOperationException("Engine not initialized");
            var ocrText = Marshal.PtrToStringUTF8(ocrTextPtr) ?? "";
            return await _engine.ValidateIdentityAsync(ocrText);
        });
    }

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_validate_identity")]
    public static IntPtr ValidateIdentity(IntPtr ocrTextPtr) => ValidateIdentity_Impl(ocrTextPtr);

    internal static IntPtr SanitizeText_Impl(IntPtr ocrTextPtr)
    {
        try
        {
            if (_engine == null) throw new InvalidOperationException("Engine not initialized");
            var ocrText = Marshal.PtrToStringUTF8(ocrTextPtr) ?? "";
            var result = _engine.SanitizeText(ocrText);
            return AllocateString(result);
        }
        catch
        {
            return AllocateString(Marshal.PtrToStringUTF8(ocrTextPtr) ?? "");
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_sanitize_text")]
    public static IntPtr SanitizeText(IntPtr ocrTextPtr) => SanitizeText_Impl(ocrTextPtr);

    internal static IntPtr ExtractStructured_Impl(IntPtr ocrTextPtr, IntPtr docTypePtr)
    {
        try
        {
            if (_engine == null) throw new InvalidOperationException("Engine not initialized");
            var ocrText = Marshal.PtrToStringUTF8(ocrTextPtr) ?? "";
            var docType = Marshal.PtrToStringUTF8(docTypePtr) ?? "Unknown";
            var result = _engine.ExtractStructured(ocrText, docType);
            return AllocateString(result);
        }
        catch (Exception ex)
        {
            return AllocateString(JsonSerializer.Serialize(
                new OperationResultDto { Success = false, Error = ex.Message },
                MobileJsonContext.Default.OperationResultDto));
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_extract_structured")]
    public static IntPtr ExtractStructured(IntPtr ocrTextPtr, IntPtr docTypePtr)
        => ExtractStructured_Impl(ocrTextPtr, docTypePtr);

    internal static IntPtr ProcessFullOcr_Impl(IntPtr ocrTextPtr)
    {
        return ExecuteAsync(async () =>
        {
            if (_engine == null) throw new InvalidOperationException("Engine not initialized");
            var ocrText = Marshal.PtrToStringUTF8(ocrTextPtr) ?? "";
            return await _engine.ProcessFullOcrAsync(ocrText);
        });
    }

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_process_full_ocr")]
    public static IntPtr ProcessFullOcr(IntPtr ocrTextPtr) => ProcessFullOcr_Impl(ocrTextPtr);

    internal static IntPtr SaveOcrDocument_Impl(IntPtr inputJsonPtr)
    {
        return ExecuteAsync(async () =>
        {
            if (_engine == null) throw new InvalidOperationException("Engine not initialized");
            var json = Marshal.PtrToStringUTF8(inputJsonPtr) ?? throw new ArgumentNullException(nameof(inputJsonPtr));
            return await _engine.SaveOcrDocumentAsync(json);
        });
    }

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_save_ocr_document")]
    public static IntPtr SaveOcrDocument(IntPtr inputJsonPtr) => SaveOcrDocument_Impl(inputJsonPtr);

    // ═══════════════════════════════════════════════════════════════════════════
    //  Advanced OCR — Vault, Encryption, Structured Extraction
    // ═══════════════════════════════════════════════════════════════════════════

    internal static IntPtr VaultInitialize_Impl(IntPtr inputJsonPtr)
    {
        return ExecuteSync(() =>
        {
            if (_engine == null) throw new InvalidOperationException("Engine not initialized");
            var json = Marshal.PtrToStringUTF8(inputJsonPtr) ?? "";
            return _engine.VaultInitialize(json);
        });
    }

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_vault_initialize")]
    public static IntPtr VaultInitialize(IntPtr inputJsonPtr) => VaultInitialize_Impl(inputJsonPtr);

    internal static IntPtr VaultUnlock_Impl(IntPtr masterKeyB64Ptr)
    {
        return ExecuteSync(() =>
        {
            if (_engine == null) throw new InvalidOperationException("Engine not initialized");
            var key = Marshal.PtrToStringUTF8(masterKeyB64Ptr) ?? "";
            return _engine.VaultUnlock(key);
        });
    }

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_vault_unlock")]
    public static IntPtr VaultUnlock(IntPtr masterKeyB64Ptr) => VaultUnlock_Impl(masterKeyB64Ptr);

    internal static IntPtr VaultLock_Impl()
    {
        return ExecuteSync(() =>
        {
            if (_engine == null) throw new InvalidOperationException("Engine not initialized");
            return _engine.VaultLock();
        });
    }

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_vault_lock")]
    public static IntPtr VaultLock() => VaultLock_Impl();

    internal static IntPtr VaultStoreDocument_Impl(IntPtr inputJsonPtr)
    {
        return ExecuteAsync(async () =>
        {
            if (_engine == null) throw new InvalidOperationException("Engine not initialized");
            var json = Marshal.PtrToStringUTF8(inputJsonPtr) ?? "";
            return await _engine.VaultStoreDocumentAsync(json);
        });
    }

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_vault_store_document")]
    public static IntPtr VaultStoreDocument(IntPtr inputJsonPtr) => VaultStoreDocument_Impl(inputJsonPtr);

    internal static IntPtr VaultRetrieveDocument_Impl(IntPtr docIdPtr)
    {
        return ExecuteAsync(async () =>
        {
            if (_engine == null) throw new InvalidOperationException("Engine not initialized");
            var docId = Marshal.PtrToStringUTF8(docIdPtr) ?? "";
            return await _engine.VaultRetrieveDocumentAsync(docId);
        });
    }

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_vault_retrieve_document")]
    public static IntPtr VaultRetrieveDocument(IntPtr docIdPtr) => VaultRetrieveDocument_Impl(docIdPtr);

    internal static IntPtr VaultDeleteDocument_Impl(IntPtr docIdPtr)
    {
        return ExecuteSync(() =>
        {
            if (_engine == null) throw new InvalidOperationException("Engine not initialized");
            var docId = Marshal.PtrToStringUTF8(docIdPtr) ?? "";
            return _engine.VaultDeleteDocument(docId);
        });
    }

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_vault_delete_document")]
    public static IntPtr VaultDeleteDocument(IntPtr docIdPtr) => VaultDeleteDocument_Impl(docIdPtr);

    internal static IntPtr VaultWipe_Impl()
    {
        return ExecuteSync(() =>
        {
            if (_engine == null) throw new InvalidOperationException("Engine not initialized");
            return _engine.VaultWipe();
        });
    }

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_vault_wipe")]
    public static IntPtr VaultWipe() => VaultWipe_Impl();

    internal static IntPtr ClassifyWithOrchestrator_Impl(IntPtr ocrTextPtr, IntPtr mlTypePtr, float mlConfidence)
    {
        return ExecuteSync(() =>
        {
            if (_engine == null) throw new InvalidOperationException("Engine not initialized");
            var ocrText = Marshal.PtrToStringUTF8(ocrTextPtr) ?? "";
            var mlType = Marshal.PtrToStringUTF8(mlTypePtr);
            return _engine.ClassifyWithOrchestrator(ocrText, mlType, mlConfidence);
        });
    }

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_classify_with_orchestrator")]
    public static IntPtr ClassifyWithOrchestrator(IntPtr ocrTextPtr, IntPtr mlTypePtr, float mlConfidence)
        => ClassifyWithOrchestrator_Impl(ocrTextPtr, mlTypePtr, mlConfidence);

    internal static IntPtr BuildStructuredDocument_Impl(IntPtr ocrTextPtr, IntPtr docTypePtr, float classConfidence, IntPtr classMethodPtr)
    {
        return ExecuteSync(() =>
        {
            if (_engine == null) throw new InvalidOperationException("Engine not initialized");
            var ocrText = Marshal.PtrToStringUTF8(ocrTextPtr) ?? "";
            var docType = Marshal.PtrToStringUTF8(docTypePtr) ?? "Unknown";
            var classMethod = Marshal.PtrToStringUTF8(classMethodPtr) ?? "";
            return _engine.BuildStructuredDocument(ocrText, docType, classConfidence, classMethod);
        });
    }

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_build_structured_document")]
    public static IntPtr BuildStructuredDocument(IntPtr ocrTextPtr, IntPtr docTypePtr, float classConfidence, IntPtr classMethodPtr)
        => BuildStructuredDocument_Impl(ocrTextPtr, docTypePtr, classConfidence, classMethodPtr);

    internal static IntPtr BuildStructuredDocumentFromJson_Impl(IntPtr inputJsonPtr)
    {
        return ExecuteSync(() =>
        {
            if (_engine == null) throw new InvalidOperationException("Engine not initialized");
            var json = Marshal.PtrToStringUTF8(inputJsonPtr) ?? "";
            return _engine.BuildStructuredDocumentFromJson(json);
        });
    }

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_build_structured_document_json")]
    public static IntPtr BuildStructuredDocumentFromJson(IntPtr inputJsonPtr)
        => BuildStructuredDocumentFromJson_Impl(inputJsonPtr);

    internal static IntPtr GetMlAuditSummary_Impl()
    {
        return ExecuteSync(() =>
        {
            if (_engine == null) throw new InvalidOperationException("Engine not initialized");
            return _engine.GetMlAuditSummary();
        });
    }

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_get_ml_audit_summary")]
    public static IntPtr GetMlAuditSummary() => GetMlAuditSummary_Impl();

    internal static IntPtr ValidateDocument_Impl(IntPtr headerB64Ptr, IntPtr extensionPtr, long fileSizeBytes)
    {
        return ExecuteSync(() =>
        {
            if (_engine == null) throw new InvalidOperationException("Engine not initialized");
            var headerB64 = Marshal.PtrToStringUTF8(headerB64Ptr) ?? "";
            var extension = Marshal.PtrToStringUTF8(extensionPtr) ?? "";
            return _engine.ValidateDocument(headerB64, extension, fileSizeBytes);
        });
    }

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_validate_document")]
    public static IntPtr ValidateDocument(IntPtr headerB64Ptr, IntPtr extensionPtr, long fileSizeBytes)
        => ValidateDocument_Impl(headerB64Ptr, extensionPtr, fileSizeBytes);

    // ═══════════════════════════════════════════════════════════════════════════
    //  Doctor Assignments
    // ═══════════════════════════════════════════════════════════════════════════

    internal static IntPtr GetAssignedDoctors_Impl()
    {
        return ExecuteAsync(async () =>
        {
            if (_engine == null) throw new InvalidOperationException("Engine not initialized");
            return await _engine.GetAssignedDoctorsAsync();
        });
    }

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_get_assigned_doctors")]
    public static IntPtr GetAssignedDoctors() => GetAssignedDoctors_Impl();

    // ═══════════════════════════════════════════════════════════════════════════
    //  Local Data Reset
    // ═══════════════════════════════════════════════════════════════════════════

    internal static IntPtr ResetLocalData_Impl()
    {
        return ExecuteAsync(async () =>
        {
            if (_engine == null) throw new InvalidOperationException("Engine not initialized");
            return await _engine.ResetLocalDataAsync();
        });
    }

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_reset_local_data")]
    public static IntPtr ResetLocalData() => ResetLocalData_Impl();

    // ═══════════════════════════════════════════════════════════════════════════
    //  Environment Analytics
    // ═══════════════════════════════════════════════════════════════════════════

    internal static IntPtr GetEnvironmentAnalytics_Impl()
    {
        return ExecuteAsync(async () =>
        {
            if (_engine == null) throw new InvalidOperationException("Engine not initialized");
            return await _engine.GetEnvironmentAnalyticsAsync();
        });
    }

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_get_environment_analytics")]
    public static IntPtr GetEnvironmentAnalytics() => GetEnvironmentAnalytics_Impl();

    // ═══════════════════════════════════════════════════════════════════════════
    //  Environment Coaching Advice
    // ═══════════════════════════════════════════════════════════════════════════

    internal static IntPtr GetEnvironmentAdvice_Impl()
    {
        return ExecuteAsync(async () =>
        {
            if (_engine == null) throw new InvalidOperationException("Engine not initialized");
            return await _engine.GetEnvironmentAdviceAsync();
        });
    }

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_get_environment_advice")]
    public static IntPtr GetEnvironmentAdvice() => GetEnvironmentAdvice_Impl();

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
            return AllocateString(JsonSerializer.Serialize(new OperationResultDto { Success = false, Error = $"Operation failed: {ExtractErrorMessage(ex)}" }, MobileJsonContext.Default.OperationResultDto));
        }
    }

    private static IntPtr ExecuteSync(Func<string> operation)
    {
        try
        {
            return AllocateString(operation());
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[NativeBridge] Sync operation failed");
            return AllocateString(JsonSerializer.Serialize(
                new OperationResultDto { Success = false, Error = $"Operation failed: {ExtractErrorMessage(ex)}" },
                MobileJsonContext.Default.OperationResultDto));
        }
    }

    private static string ExtractErrorMessage(Exception ex)
    {
        if (ex is AggregateException aggregate)
        {
            var flattened = aggregate.Flatten();
            if (flattened.InnerExceptions.Count == 1)
                return flattened.InnerExceptions[0].Message;
        }

        return ex.Message;
    }

    private static void EnsureGlobalExceptionHandlers()
    {
        if (Interlocked.Exchange(ref _globalExceptionHandlersInstalled, 1) == 1)
            return;

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            try
            {
                var ex = args.ExceptionObject as Exception;
                _logger?.LogCritical(ex, "[NativeBridge] Unhandled exception (IsTerminating={IsTerminating})", args.IsTerminating);
                Console.Error.WriteLine($"[NativeBridge] Unhandled exception (IsTerminating={args.IsTerminating}): {ex?.ToString() ?? args.ExceptionObject?.ToString() ?? "unknown"}");
            }
            catch
            {
                // Never throw from global exception handlers.
            }
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            try
            {
                _logger?.LogError(args.Exception, "[NativeBridge] Unobserved task exception captured");
                Console.Error.WriteLine($"[NativeBridge] Unobserved task exception: {args.Exception}");
            }
            catch
            {
                // Never throw from global exception handlers.
            }

            args.SetObserved();
        };
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