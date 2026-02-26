using MudBlazor;

namespace DigitalTwin.Components.Theme;

public static class MedicalTheme
{
    public static MudTheme Instance { get; } = new()
    {
        PaletteLight = new PaletteLight()
        {
            Primary = "#009688",
            Secondary = "#FF2D55",
            AppbarBackground = "#009688"
        },
        PaletteDark = new PaletteDark()
        {
            Primary = "#00D4C8",
            Secondary = "#FF2D55",
            Surface = "#0F1525",
            Background = "#0A0E1A",
            AppbarBackground = "rgba(10, 14, 26, 0.7)",
            DrawerBackground = "rgba(10, 15, 26, 0.95)",
            TextPrimary = "rgba(255,255,255,0.92)",
            TextSecondary = "rgba(255,255,255,0.52)"
        },
        Typography = new Typography()
        {
            Default = new DefaultTypography()
            {
                FontFamily = ["-apple-system", "SF Pro Text", "Helvetica Neue", "sans-serif"]
            },
        H3 = new H3Typography()
        {
            FontFamily = ["-apple-system", "SF Pro Rounded", "Helvetica Neue", "sans-serif"],
            FontWeight = "300"
        },
        H4 = new H4Typography()
        {
            FontFamily = ["-apple-system", "SF Pro Display", "Helvetica Neue", "sans-serif"],
            FontWeight = "700"
        },
        H5 = new H5Typography()
        {
            FontFamily = ["-apple-system", "SF Pro Display", "Helvetica Neue", "sans-serif"],
            FontWeight = "700"
        },
        H6 = new H6Typography()
        {
            FontFamily = ["-apple-system", "SF Pro Display", "Helvetica Neue", "sans-serif"],
            FontWeight = "600"
        }
        },
        LayoutProperties = new LayoutProperties()
        {
            DefaultBorderRadius = "32px"
        }
    };
}
