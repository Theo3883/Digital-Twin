using CoreGraphics;
using Foundation;
using UIKit;
using WebKit;

namespace DigitalTwin;

/// <summary>
/// WKWebView subclass that suppresses the iOS keyboard accessory bar
/// (the ▲ ▼ ✓ toolbar shown above the keyboard for web inputs) on all devices.
/// Overriding <see cref="InputAccessoryView"/> at the native level is the only
/// fully reliable, version-independent way to hide it.
/// </summary>
[Register("NoAccessoryWebView")]
internal sealed class NoAccessoryWebView : WKWebView
{
    public NoAccessoryWebView(CGRect frame, WKWebViewConfiguration configuration)
        : base(frame, configuration)
    {
        // Hide iOS scroll indicators for this web view
        ScrollView.ShowsVerticalScrollIndicator = false;
        ScrollView.ShowsHorizontalScrollIndicator = false;
        // Disable pinch-to-zoom and double-tap zoom
        ScrollView.MinimumZoomScale = 1;
        ScrollView.MaximumZoomScale = 1;
    }

    /// <summary>
    /// Returning null removes the accessory bar entirely.
    /// </summary>
    public override UIView InputAccessoryView => null!;
}
