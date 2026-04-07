using DigitalTwin.Domain.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DigitalTwin;

public partial class App : Microsoft.Maui.Controls.Application
{
    private readonly AppDebugLogger<App> _logger;
    private readonly IServiceProvider _services;

    public App(AppDebugLogger<App> logger, IServiceProvider services)
    {
        _logger = logger;
        _services = services;
        InitializeComponent();
        RegisterGlobalExceptionHandlers();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(_services.GetRequiredService<MainPage>()) { Title = "DigitalTwin" };
    }

    // ── Global exception safety net ──────────────────────────────────────────

    private void RegisterGlobalExceptionHandlers()
    {
        // .NET thread-pool / background task exceptions
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            _logger.Error(ex ?? new Exception("Unknown"), "[App] Unhandled domain exception (terminating={T}).", e.IsTerminating);
            ShowErrorToast(ex?.Message ?? "An unexpected error occurred.");
        };

        // async Task exceptions that were never awaited
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            _logger.Error(e.Exception, "[App] Unobserved task exception.");
            e.SetObserved();   // prevent process termination
        };
    }

    private static void ShowErrorToast(string message)
    {
        // Show a non-intrusive toast on the UI thread; swallow any further errors.
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                if (Current?.Windows[0]?.Page is Page page)
                    await page.DisplayAlertAsync("Something went wrong", message, "OK");
            }
            catch { /* last resort — ignore */ }
        });
    }
}
