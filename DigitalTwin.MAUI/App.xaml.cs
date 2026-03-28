using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DigitalTwin;

public partial class App : Microsoft.Maui.Controls.Application
{
    private readonly ILogger<App> _logger;
    private readonly IServiceProvider _services;

    public App(ILogger<App> logger, IServiceProvider services)
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
            _logger.LogCritical(ex, "[App] Unhandled domain exception (terminating={T}).", e.IsTerminating);
            ShowErrorToast(ex?.Message ?? "An unexpected error occurred.");
        };

        // async Task exceptions that were never awaited
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            _logger.LogError(e.Exception, "[App] Unobserved task exception.");
            e.SetObserved();   // prevent process termination
        };
    }

    private void ShowErrorToast(string message)
    {
        // Show a non-intrusive toast on the UI thread; swallow any further errors.
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                if (Current?.Windows.FirstOrDefault()?.Page is Page page)
                    await page.DisplayAlert("Something went wrong", message, "OK");
            }
            catch { /* last resort — ignore */ }
        });
    }
}
