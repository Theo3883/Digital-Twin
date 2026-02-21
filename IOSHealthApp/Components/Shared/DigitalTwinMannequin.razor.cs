using Microsoft.AspNetCore.Components;

namespace IOSHealthApp.Components.Shared;

public partial class DigitalTwinMannequin : ComponentBase
{
    [Parameter]
    public double HeartRateValue { get; set; } = 72;

    [Parameter]
    public double OxygenValue { get; set; } = 97;

    [Parameter]
    public double StepsValue { get; set; } = 0;

    protected string BodyColor => "#4a5568";

    protected string PulseDuration => $"{60.0 / Math.Max(HeartRateValue, 40):F2}s";

    protected string HeartColor => HeartRateValue switch
    {
        > 120 => "#ef4444",
        > 100 => "#f59e0b",
        _ => "#4ade80"
    };

    protected string LungColor => OxygenValue switch
    {
        < 90 => "#ef4444",
        < 95 => "#f59e0b",
        _ => "#60a5fa"
    };

    protected string ActivityColor => StepsValue switch
    {
        > 8000 => "#4ade80",
        > 3000 => "#fbbf24",
        _ => "#6b7280"
    };

    protected string ActivityOpacity => StepsValue switch
    {
        > 8000 => "0.3",
        > 3000 => "0.2",
        _ => "0.1"
    };

    protected string ActivityLevel => StepsValue switch
    {
        > 8000 => "Active",
        > 3000 => "Moderate",
        _ => "Low"
    };

    protected string ContainerStyle => "display: flex; flex-direction: column; align-items: center; padding: 20px; background: linear-gradient(145deg, rgba(26, 31, 46, 0.9), rgba(15, 20, 35, 0.95)); border: 1px solid rgba(255, 255, 255, 0.06); border-radius: 16px; backdrop-filter: blur(20px);";
}
