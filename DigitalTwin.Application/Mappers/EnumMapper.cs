using AppEnums = DigitalTwin.Application.Enums;
using DomainEnums = DigitalTwin.Domain.Enums;

namespace DigitalTwin.Application.Mappers;

/// <summary>
/// Converts aligned enum values between the domain and application layers.
/// </summary>
public static class EnumMapper
{
    /// <summary>
    /// Converts a domain vital-sign type to the application enum.
    /// </summary>
    public static AppEnums.VitalSignType ToApp(DomainEnums.VitalSignType type)
        => (AppEnums.VitalSignType)(int)type;

    /// <summary>
    /// Converts a domain air-quality level to the application enum.
    /// </summary>
    public static AppEnums.AirQualityLevel ToApp(DomainEnums.AirQualityLevel level)
        => (AppEnums.AirQualityLevel)(int)level;

    /// <summary>
    /// Converts a domain interaction severity to the application enum.
    /// </summary>
    public static AppEnums.InteractionSeverity ToApp(DomainEnums.InteractionSeverity severity)
        => (AppEnums.InteractionSeverity)(int)severity;

    /// <summary>
    /// Converts an application vital-sign type to the domain enum.
    /// </summary>
    public static DomainEnums.VitalSignType ToDomain(AppEnums.VitalSignType type)
        => (DomainEnums.VitalSignType)(int)type;

    public static DomainEnums.AirQualityLevel ToDomain(AppEnums.AirQualityLevel level)
        => (DomainEnums.AirQualityLevel)(int)level;
}
