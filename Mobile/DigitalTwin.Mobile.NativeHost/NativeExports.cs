using System.Runtime.InteropServices;
using DigitalTwin.Mobile.Engine;

namespace DigitalTwin.Mobile.NativeHost;

/// <summary>
/// C ABI exports consumed by Swift.
/// These forward into <see cref="DigitalTwin.Mobile.Engine.NativeBridge"/>.
/// </summary>
public static class NativeExports
{
    // ── Lifecycle ─────────────────────────────────────────────────────────────

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_initialize")]
    public static IntPtr Initialize(IntPtr databasePathPtr, IntPtr apiBaseUrlPtr, IntPtr geminiApiKeyPtr, IntPtr openWeatherApiKeyPtr, IntPtr googleOAuthClientIdPtr, IntPtr openRouterApiKeyPtr, IntPtr openRouterModelPtr)
        => NativeBridge.Initialize_Impl(databasePathPtr, apiBaseUrlPtr, geminiApiKeyPtr, openWeatherApiKeyPtr, googleOAuthClientIdPtr, openRouterApiKeyPtr, openRouterModelPtr);

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_initialize_database")]
    public static IntPtr InitializeDatabase()
        => NativeBridge.InitializeDatabase_Impl();

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_dispose")]
    public static void Dispose()
        => NativeBridge.Dispose_Impl();

    // ── Auth ──────────────────────────────────────────────────────────────────

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_authenticate")]
    public static IntPtr Authenticate(IntPtr googleIdTokenPtr)
        => NativeBridge.Authenticate_Impl(googleIdTokenPtr);

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_get_current_user")]
    public static IntPtr GetCurrentUser()
        => NativeBridge.GetCurrentUser_Impl();

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_update_current_user")]
    public static IntPtr UpdateCurrentUser(IntPtr updateJsonPtr)
        => NativeBridge.UpdateCurrentUser_Impl(updateJsonPtr);

    // ── Patient Profile ───────────────────────────────────────────────────────

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_get_patient_profile")]
    public static IntPtr GetPatientProfile()
        => NativeBridge.GetPatientProfile_Impl();

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_update_patient_profile")]
    public static IntPtr UpdatePatientProfile(IntPtr updateJsonPtr)
        => NativeBridge.UpdatePatientProfile_Impl(updateJsonPtr);

    // ── Vital Signs ───────────────────────────────────────────────────────────

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_record_vital_sign")]
    public static IntPtr RecordVitalSign(IntPtr vitalSignJsonPtr)
        => NativeBridge.RecordVitalSign_Impl(vitalSignJsonPtr);

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_record_vital_signs")]
    public static IntPtr RecordVitalSigns(IntPtr vitalSignsJsonPtr)
        => NativeBridge.RecordVitalSigns_Impl(vitalSignsJsonPtr);

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_get_vital_signs")]
    public static IntPtr GetVitalSigns(IntPtr fromDateIsoPtr, IntPtr toDateIsoPtr)
        => NativeBridge.GetVitalSigns_Impl(fromDateIsoPtr, toDateIsoPtr);

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_get_vital_signs_by_type")]
    public static IntPtr GetVitalSignsByType(int vitalTypeInt, IntPtr fromDateIsoPtr, IntPtr toDateIsoPtr)
        => NativeBridge.GetVitalSignsByType_Impl(vitalTypeInt, fromDateIsoPtr, toDateIsoPtr);

    // ── Sync ──────────────────────────────────────────────────────────────────

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_perform_sync")]
    public static IntPtr PerformSync()
        => NativeBridge.PerformSync_Impl();

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_push_local_changes")]
    public static IntPtr PushLocalChanges()
        => NativeBridge.PushLocalChanges_Impl();

    // ── Cloud session restore ────────────────────────────────────────────────

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_set_cloud_access_token")]
    public static IntPtr SetCloudAccessToken(IntPtr tokenPtr)
        => NativeBridge.SetCloudAccessToken_Impl(tokenPtr);

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_get_cloud_auth_status")]
    public static IntPtr GetCloudAuthStatus()
        => NativeBridge.GetCloudAuthStatus_Impl();

    // ── Medications ───────────────────────────────────────────────────────────

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_get_medications")]
    public static IntPtr GetMedications()
        => NativeBridge.GetMedications_Impl();

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_add_medication")]
    public static IntPtr AddMedication(IntPtr inputJsonPtr)
        => NativeBridge.AddMedication_Impl(inputJsonPtr);

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_discontinue_medication")]
    public static IntPtr DiscontinueMedication(IntPtr inputJsonPtr)
        => NativeBridge.DiscontinueMedication_Impl(inputJsonPtr);

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_search_drugs")]
    public static IntPtr SearchDrugs(IntPtr queryPtr)
        => NativeBridge.SearchDrugs_Impl(queryPtr);

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_check_interactions")]
    public static IntPtr CheckInteractions(IntPtr rxCuisJsonPtr)
        => NativeBridge.CheckInteractions_Impl(rxCuisJsonPtr);

    // ── Environment ───────────────────────────────────────────────────────────

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_get_environment_reading")]
    public static IntPtr GetEnvironmentReading(double latitude, double longitude)
        => NativeBridge.GetEnvironmentReading_Impl(latitude, longitude);

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_get_latest_environment_reading")]
    public static IntPtr GetLatestEnvironmentReading()
        => NativeBridge.GetLatestEnvironmentReading_Impl();

    // ── ECG ───────────────────────────────────────────────────────────────────

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_evaluate_ecg_frame")]
    public static IntPtr EvaluateEcgFrame(IntPtr frameJsonPtr)
        => NativeBridge.EvaluateEcgFrame_Impl(frameJsonPtr);

    // ── AI Chat ───────────────────────────────────────────────────────────────

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_send_chat_message")]
    public static IntPtr SendChatMessage(IntPtr messagePtr)
        => NativeBridge.SendChatMessage_Impl(messagePtr);

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_get_chat_history")]
    public static IntPtr GetChatHistory()
        => NativeBridge.GetChatHistory_Impl();

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_clear_chat_history")]
    public static IntPtr ClearChatHistory()
        => NativeBridge.ClearChatHistory_Impl();

    // ── Coaching ──────────────────────────────────────────────────────────────

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_get_coaching_advice")]
    public static IntPtr GetCoachingAdvice()
        => NativeBridge.GetCoachingAdvice_Impl();

    // ── Sleep ─────────────────────────────────────────────────────────────────

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_record_sleep_session")]
    public static IntPtr RecordSleepSession(IntPtr sessionJsonPtr)
        => NativeBridge.RecordSleepSession_Impl(sessionJsonPtr);

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_get_sleep_sessions")]
    public static IntPtr GetSleepSessions(IntPtr fromDateIsoPtr, IntPtr toDateIsoPtr)
        => NativeBridge.GetSleepSessions_Impl(fromDateIsoPtr, toDateIsoPtr);

    // ── Medical History & OCR ─────────────────────────────────────────────────

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_get_medical_history")]
    public static IntPtr GetMedicalHistory()
        => NativeBridge.GetMedicalHistory_Impl();

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_get_ocr_documents")]
    public static IntPtr GetOcrDocuments()
        => NativeBridge.GetOcrDocuments_Impl();

    // ── OCR Text Processing ───────────────────────────────────────────────────

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_classify_document")]
    public static IntPtr ClassifyDocument(IntPtr ocrTextPtr)
        => NativeBridge.ClassifyDocument_Impl(ocrTextPtr);

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_extract_identity")]
    public static IntPtr ExtractIdentity(IntPtr ocrTextPtr)
        => NativeBridge.ExtractIdentity_Impl(ocrTextPtr);

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_validate_identity")]
    public static IntPtr ValidateIdentity(IntPtr ocrTextPtr)
        => NativeBridge.ValidateIdentity_Impl(ocrTextPtr);

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_sanitize_text")]
    public static IntPtr SanitizeText(IntPtr ocrTextPtr)
        => NativeBridge.SanitizeText_Impl(ocrTextPtr);

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_extract_structured")]
    public static IntPtr ExtractStructured(IntPtr ocrTextPtr, IntPtr docTypePtr)
        => NativeBridge.ExtractStructured_Impl(ocrTextPtr, docTypePtr);

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_process_full_ocr")]
    public static IntPtr ProcessFullOcr(IntPtr ocrTextPtr)
        => NativeBridge.ProcessFullOcr_Impl(ocrTextPtr);

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_save_ocr_document")]
    public static IntPtr SaveOcrDocument(IntPtr inputJsonPtr)
        => NativeBridge.SaveOcrDocument_Impl(inputJsonPtr);

    // ── Advanced OCR / Vault ────────────────────────────────────────────────

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_vault_initialize")]
    public static IntPtr VaultInitialize(IntPtr inputJsonPtr)
        => NativeBridge.VaultInitialize_Impl(inputJsonPtr);

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_vault_unlock")]
    public static IntPtr VaultUnlock(IntPtr masterKeyB64Ptr)
        => NativeBridge.VaultUnlock_Impl(masterKeyB64Ptr);

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_vault_lock")]
    public static IntPtr VaultLock()
        => NativeBridge.VaultLock_Impl();

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_vault_store_document")]
    public static IntPtr VaultStoreDocument(IntPtr inputJsonPtr)
        => NativeBridge.VaultStoreDocument_Impl(inputJsonPtr);

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_vault_retrieve_document")]
    public static IntPtr VaultRetrieveDocument(IntPtr docIdPtr)
        => NativeBridge.VaultRetrieveDocument_Impl(docIdPtr);

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_vault_delete_document")]
    public static IntPtr VaultDeleteDocument(IntPtr docIdPtr)
        => NativeBridge.VaultDeleteDocument_Impl(docIdPtr);

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_vault_wipe")]
    public static IntPtr VaultWipe()
        => NativeBridge.VaultWipe_Impl();

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_classify_with_orchestrator")]
    public static IntPtr ClassifyWithOrchestrator(IntPtr ocrTextPtr, IntPtr mlTypePtr, float mlConfidence)
        => NativeBridge.ClassifyWithOrchestrator_Impl(ocrTextPtr, mlTypePtr, mlConfidence);

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_build_structured_document")]
    public static IntPtr BuildStructuredDocument(IntPtr ocrTextPtr, IntPtr docTypePtr, float classConfidence, IntPtr classMethodPtr)
        => NativeBridge.BuildStructuredDocument_Impl(ocrTextPtr, docTypePtr, classConfidence, classMethodPtr);

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_build_structured_document_json")]
    public static IntPtr BuildStructuredDocumentFromJson(IntPtr inputJsonPtr)
        => NativeBridge.BuildStructuredDocumentFromJson_Impl(inputJsonPtr);

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_get_ml_audit_summary")]
    public static IntPtr GetMlAuditSummary()
        => NativeBridge.GetMlAuditSummary_Impl();

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_validate_document")]
    public static IntPtr ValidateDocument(IntPtr headerB64Ptr, IntPtr extensionPtr, long fileSizeBytes)
        => NativeBridge.ValidateDocument_Impl(headerB64Ptr, extensionPtr, fileSizeBytes);

    // ── Memory Management ─────────────────────────────────────────────────────

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_free_string")]
    public static void FreeString(IntPtr ptr)
        => NativeBridge.FreeString_Impl(ptr);

    // ── Doctor Assignments ────────────────────────────────────────────────────

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_get_assigned_doctors")]
    public static IntPtr GetAssignedDoctors()
        => NativeBridge.GetAssignedDoctors_Impl();

    // ── Notifications ─────────────────────────────────────────────────────────

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_get_notifications")]
    public static IntPtr GetNotifications(int limit, bool unreadOnly)
        => NativeBridge.GetNotifications_Impl(limit, unreadOnly);

    // ── Local Data Reset ──────────────────────────────────────────────────────

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_reset_local_data")]
    public static IntPtr ResetLocalData()
        => NativeBridge.ResetLocalData_Impl();

    // ── Environment Analytics ─────────────────────────────────────────────────

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_get_environment_analytics")]
    public static IntPtr GetEnvironmentAnalytics()
        => NativeBridge.GetEnvironmentAnalytics_Impl();

    // ── Environment Coaching Advice ───────────────────────────────────────────

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_get_environment_advice")]
    public static IntPtr GetEnvironmentAdvice()
        => NativeBridge.GetEnvironmentAdvice_Impl();
}