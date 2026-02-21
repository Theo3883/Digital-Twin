using Foundation;
using UIKit;
namespace DigitalTwin;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    public override bool OpenUrl(UIApplication app, NSUrl url, NSDictionary options)
    {
        if (Microsoft.Maui.ApplicationModel.Platform.OpenUrl(app, url, options))
            return true;
        
        return base.OpenUrl(app, url, options);
    }
}
