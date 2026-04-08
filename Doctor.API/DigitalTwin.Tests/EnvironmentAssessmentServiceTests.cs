using DigitalTwin.Domain.Enums;
using DigitalTwin.Domain.Models;
using DigitalTwin.Domain.Services;

namespace DigitalTwin.Tests;

public class EnvironmentAssessmentServiceTests
{
    private readonly EnvironmentAssessmentService _sut = new();

    [Theory]
    [InlineData(10, AirQualityLevel.Good)]
    [InlineData(50, AirQualityLevel.Good)]
    [InlineData(51, AirQualityLevel.Moderate)]
    [InlineData(100, AirQualityLevel.Moderate)]
    [InlineData(101, AirQualityLevel.Unhealthy)]
    [InlineData(200, AirQualityLevel.Unhealthy)]
    public void DetermineAirQuality_MapsCorrectly(double pm25, AirQualityLevel expected)
    {
        Assert.Equal(expected, _sut.DetermineAirQuality(pm25));
    }

    [Fact]
    public void AssessReading_SetsAirQualityFromPm25()
    {
        var reading = new EnvironmentReading
        {
            PM25 = 75,
            Temperature = 22,
            Humidity = 55
        };

        var assessed = _sut.AssessReading(reading);

        Assert.Equal(AirQualityLevel.Moderate, assessed.AirQuality);
        Assert.Same(reading, assessed);
    }

    [Fact]
    public void AssessReading_UnhealthyAirForHighPm25()
    {
        var reading = new EnvironmentReading { PM25 = 150 };
        var assessed = _sut.AssessReading(reading);
        Assert.Equal(AirQualityLevel.Unhealthy, assessed.AirQuality);
    }

    [Fact]
    public void AssessReading_GoodAirForLowPm25()
    {
        var reading = new EnvironmentReading { PM25 = 25 };
        var assessed = _sut.AssessReading(reading);
        Assert.Equal(AirQualityLevel.Good, assessed.AirQuality);
    }
}
