using BackgroundTasks;
using DigitalTwin.Application.Interfaces;
using Foundation;
using UIKit;

namespace DigitalTwin;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    /// <summary>
    /// Unique task identifier registered in Info.plist → BGTaskSchedulerPermittedIdentifiers.
    /// </summary>
    private const string BackgroundSyncTaskId = "com.digitaltwin.health.sync";

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    public override bool FinishedLaunching(UIApplication application, NSDictionary launchOptions)
    {
        var result = base.FinishedLaunching(application, launchOptions);

        // Register the background refresh task.
        BGTaskScheduler.Shared.Register(BackgroundSyncTaskId, null, task =>
        {
            HandleBackgroundSync((BGAppRefreshTask)task);
        });

        return result;
    }

    public override bool OpenUrl(UIApplication app, NSUrl url, NSDictionary options)
    {
        if (Microsoft.Maui.ApplicationModel.Platform.OpenUrl(app, url, options))
            return true;

        return base.OpenUrl(app, url, options);
    }

    /// <summary>
    /// Called when the app transitions to the background. Schedules the next
    /// background refresh so iOS wakes the app periodically.
    /// </summary>
    public override void DidEnterBackground(UIApplication application)
    {
        base.DidEnterBackground(application);
        ScheduleBackgroundSync();
    }

    // ── Background fetch plumbing ─────────────────────────────────────────────

    private static void ScheduleBackgroundSync()
    {
        var request = new BGAppRefreshTaskRequest(BackgroundSyncTaskId)
        {
            EarliestBeginDate = (NSDate)DateTime.UtcNow.AddMinutes(15)
        };

        try
        {
            BGTaskScheduler.Shared.Submit(request, out var error);
            if (error is not null)
                Console.WriteLine($"[BGSync] Submit error: {error}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[BGSync] Schedule failed: {ex.Message}");
        }
    }

    private void HandleBackgroundSync(BGAppRefreshTask task)
    {
        // Schedule the next occurrence before starting this one.
        ScheduleBackgroundSync();

        var cts = new CancellationTokenSource();
        task.ExpirationHandler = () => cts.Cancel();

        Task.Run(async () =>
        {
            try
            {
                var syncService = Services.GetService<IHealthDataSyncService>();
                if (syncService is not null)
                    await syncService.PushToCloudAsync();

                task.SetTaskCompleted(true);
            }
            catch
            {
                task.SetTaskCompleted(false);
            }
        }, cts.Token);
    }
}
