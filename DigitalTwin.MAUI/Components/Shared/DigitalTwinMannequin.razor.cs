using Microsoft.AspNetCore.Components;

namespace DigitalTwin.Components.Shared;

public partial class DigitalTwinMannequin : ComponentBase
{
    [Parameter]
    public double HeartRateValue { get; set; } = 72;

    [Parameter]
    public double OxygenValue { get; set; } = 97;

    [Parameter]
    public double StepsValue { get; set; } = 0;

    protected string BodyColor => "rgba(255, 255, 255, 0.35)";

    protected string PulseDuration => $"{60.0 / Math.Max(HeartRateValue, 40):F2}s";

    protected string HeartColor => HeartRateValue switch
    {
        > 120 => "#FF2D55",
        > 100 => "#FF9500",
        _ => "#00D4C8"
    };

    protected string LungColor => OxygenValue switch
    {
        < 90 => "#FF2D55",
        < 95 => "#FF9500",
        _ => "#60a5fa"
    };

    protected string ActivityColor => StepsValue switch
    {
        > 8000 => "#30D158",
        > 3000 => "#FF9500",
        _ => "rgba(255, 255, 255, 0.25)"
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

    protected string ContainerStyle => "display: flex; flex-direction: column; align-items: center; padding: 20px; border-radius: var(--radius-card); backdrop-filter: blur(28px) saturate(180%); -webkit-backdrop-filter: blur(28px) saturate(180%); background: rgba(255, 255, 255, 0.14); box-shadow: inset 0 1px 0 rgba(255, 255, 255, 0.32), inset 1px 0 0 rgba(255, 255, 255, 0.12);";
}
