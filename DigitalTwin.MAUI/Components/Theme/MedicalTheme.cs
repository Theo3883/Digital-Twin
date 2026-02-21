using MudBlazor;

namespace DigitalTwin.Components.Theme;

public static class MedicalTheme
{
    public static MudTheme Instance { get; } = new()
    {
        PaletteLight = new PaletteLight()
        {
            Primary = "#009688",
            Secondary = "#E91E63",
            AppbarBackground = "#009688"
        },
        PaletteDark = new PaletteDark()
        {
            Primary = "#26A69A",
            Secondary = "#F06292",
            Surface = "#1a1f2e",
            Background = "#0a0f1a",
            AppbarBackground = "#111827",
            DrawerBackground = "#111827",
            TextPrimary = "#e2e8f0",
            TextSecondary = "#94a3b8"
        },
        Typography = new Typography()
        {
            Default = new DefaultTypography()
            {
                FontFamily = ["Inter", "Segoe UI", "Roboto", "sans-serif"]
            }
        }
    };
}
