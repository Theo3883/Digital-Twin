namespace DigitalTwin.Integrations.Environment;

public class AirQualityData
{
    public double PM25 { get; set; }
    public double PM10 { get; set; }
    public double O3 { get; set; }
    public double NO2 { get; set; }

    /// <summary>
    /// OpenWeatherMap AQI index: 1=Good, 2=Fair, 3=Moderate, 4=Poor, 5=Very Poor.
    /// </summary>
    public int AqiIndex { get; set; }
}
