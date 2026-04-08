using System.Runtime.InteropServices;
using DigitalTwin.Mobile.Engine;

namespace DigitalTwin.Mobile.NativeHost;

/// <summary>
/// C ABI exports consumed by Swift.
/// These forward into <see cref="DigitalTwin.Mobile.Engine.NativeBridge"/>.
/// </summary>
public static class NativeExports
{
    // We keep the exported symbol names exactly the same as before so the Swift
    // side does not need to change.

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_initialize")]
    public static IntPtr Initialize(IntPtr databasePathPtr, IntPtr apiBaseUrlPtr)
        => NativeBridge.Initialize_Impl(databasePathPtr, apiBaseUrlPtr);

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_initialize_database")]
    public static IntPtr InitializeDatabase()
        => NativeBridge.InitializeDatabase_Impl();

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_dispose")]
    public static void Dispose()
        => NativeBridge.Dispose_Impl();

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_authenticate")]
    public static IntPtr Authenticate(IntPtr googleIdTokenPtr)
        => NativeBridge.Authenticate_Impl(googleIdTokenPtr);

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_get_current_user")]
    public static IntPtr GetCurrentUser()
        => NativeBridge.GetCurrentUser_Impl();

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_get_patient_profile")]
    public static IntPtr GetPatientProfile()
        => NativeBridge.GetPatientProfile_Impl();

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_update_patient_profile")]
    public static IntPtr UpdatePatientProfile(IntPtr updateJsonPtr)
        => NativeBridge.UpdatePatientProfile_Impl(updateJsonPtr);

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

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_perform_sync")]
    public static IntPtr PerformSync()
        => NativeBridge.PerformSync_Impl();

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_push_local_changes")]
    public static IntPtr PushLocalChanges()
        => NativeBridge.PushLocalChanges_Impl();

    [UnmanagedCallersOnly(EntryPoint = "mobile_engine_free_string")]
    public static void FreeString(IntPtr ptr)
        => NativeBridge.FreeString_Impl(ptr);
}

