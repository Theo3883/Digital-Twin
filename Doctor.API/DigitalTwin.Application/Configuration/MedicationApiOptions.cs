namespace DigitalTwin.Application.Configuration;

public sealed class MedicationApiOptions
{
    public const string Section = "MedicationApi";

    public string RxNavBaseUrl { get; set; } = "https://rxnav.nlm.nih.gov/REST";
    public string OpenFdaBaseUrl { get; set; } = "https://api.fda.gov/drug/label.json";
}
