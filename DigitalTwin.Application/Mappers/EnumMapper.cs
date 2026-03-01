using AppEnums = DigitalTwin.Application.Enums;
using DomainEnums = DigitalTwin.Domain.Enums;

namespace DigitalTwin.Application.Mappers;

public static class EnumMapper
{
    public static AppEnums.VitalSignType ToApp(DomainEnums.VitalSignType type)
        => (AppEnums.VitalSignType)(int)type;

    public static AppEnums.AirQualityLevel ToApp(DomainEnums.AirQualityLevel level)
        => (AppEnums.AirQualityLevel)(int)level;

    public static AppEnums.InteractionSeverity ToApp(DomainEnums.InteractionSeverity severity)
        => (AppEnums.InteractionSeverity)(int)severity;

    public static DomainEnums.VitalSignType ToDomain(AppEnums.VitalSignType type)
        => (DomainEnums.VitalSignType)(int)type;
}
