namespace DigitalTwin.Components.Shared;

public sealed record CheckDrugInteractionRequest(
    string Medication1,
    string? Medication2,
    bool IncludeActiveMedications);
