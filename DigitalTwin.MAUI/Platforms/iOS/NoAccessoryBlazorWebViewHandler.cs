using Microsoft.AspNetCore.Components.WebView.Maui;
using WebKit;

namespace DigitalTwin;

/// <summary>
/// Custom BlazorWebViewHandler that overrides CreatePlatformView to return
/// a <see cref="NoAccessoryWebView"/> instance, ensuring the iOS keyboard
/// accessory bar (▲ ▼ ✓) is suppressed at the native level on every device.
/// </summary>
internal sealed class NoAccessoryBlazorWebViewHandler : BlazorWebViewHandler
{
    protected override WKWebView CreatePlatformView()
    {
        // Let the base handler build the full WKWebViewConfiguration first.
        var baseView = base.CreatePlatformView();

        // Swap for our subclass, reusing the exact same configuration so that
        // all MAUI/Blazor internals (message handlers, interop, etc.) still work.
        var noAccessoryView = new NoAccessoryWebView(
            baseView.Frame,
            baseView.Configuration);

        // Suppress the QuickType suggestion bar above the keyboard as well.
        noAccessoryView.InputAssistantItem.LeadingBarButtonGroups = [];
        noAccessoryView.InputAssistantItem.TrailingBarButtonGroups = [];

        return noAccessoryView;
    }
}
