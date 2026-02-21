using DigitalTwin.Domain.Enums;
using DigitalTwin.Domain.Models;
using DigitalTwin.Domain.Services;

namespace DigitalTwin.Tests;

public class VitalSignServiceTests
{
    private readonly VitalSignService _sut = new();

    [Theory]
    [InlineData(80, 75, 1)]
    [InlineData(70, 75, -1)]
    [InlineData(75, 75, 0)]
    [InlineData(75.3, 75, 0)]
    public void ComputeTrend_ReturnsCorrectDirection(double current, double previous, int expected)
    {
        var trend = _sut.ComputeTrend(current, previous);
        Assert.Equal(expected, trend);
    }

    [Theory]
    [InlineData(VitalSignType.HeartRate, 72, true)]
    [InlineData(VitalSignType.HeartRate, 10, false)]
    [InlineData(VitalSignType.HeartRate, 350, false)]
    [InlineData(VitalSignType.SpO2, 97, true)]
    [InlineData(VitalSignType.SpO2, -1, false)]
    [InlineData(VitalSignType.SpO2, 101, false)]
    [InlineData(VitalSignType.Steps, 5000, true)]
    [InlineData(VitalSignType.Steps, -1, false)]
    [InlineData(VitalSignType.Calories, 0, true)]
    public void IsInValidRange_ValidatesCorrectly(VitalSignType type, double value, bool expected)
    {
        var vital = new VitalSign { Type = type, Value = value, Unit = "test" };
        Assert.Equal(expected, _sut.IsInValidRange(vital));
    }

    [Theory]
    [InlineData(VitalSignType.HeartRate, "bpm")]
    [InlineData(VitalSignType.SpO2, "%")]
    [InlineData(VitalSignType.Steps, "steps")]
    [InlineData(VitalSignType.Calories, "kcal")]
    public void GetUnitForType_ReturnsCorrectUnit(VitalSignType type, string expected)
    {
        Assert.Equal(expected, _sut.GetUnitForType(type));
    }
}
