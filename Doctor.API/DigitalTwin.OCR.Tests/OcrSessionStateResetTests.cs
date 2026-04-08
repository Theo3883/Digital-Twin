using System.Reflection;
using System.Runtime.CompilerServices;
using DigitalTwin.OCR.ViewModels;

namespace DigitalTwin.OCR.Tests;

public class OcrSessionStateResetTests
{
    [Fact]
    public void ResetSessionState_ClearsTransientState_AndRaisesStateChanged()
    {
        var vm = (OcrSessionViewModel)RuntimeHelpers.GetUninitializedObject(typeof(OcrSessionViewModel));
        var raised = false;

        SetProperty(vm, nameof(OcrSessionViewModel.IsLoading), true);
        SetProperty(vm, nameof(OcrSessionViewModel.StatusMessage), "busy");
        SetProperty(vm, nameof(OcrSessionViewModel.ErrorMessage), "File selection was cancelled.");
        SetProperty(vm, nameof(OcrSessionViewModel.SanitizedPreview), "text");
        SetProperty(vm, nameof(OcrSessionViewModel.StateChanged), (Action)(() => raised = true));

        vm.ResetSessionState();

        Assert.False(vm.IsLoading);
        Assert.Null(vm.StatusMessage);
        Assert.Null(vm.ErrorMessage);
        Assert.Null(vm.SanitizedPreview);
        Assert.True(raised);
    }

    private static void SetProperty(object target, string propertyName, object? value)
    {
        var prop = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(prop);
        prop!.SetValue(target, value);
    }
}

