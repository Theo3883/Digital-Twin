namespace DigitalTwin.Domain.Exceptions;

public class MedicationOwnershipException : DomainException
{
    public MedicationOwnershipException(Guid medicationId, Guid patientId)
        : base($"Medication '{medicationId}' does not belong to patient '{patientId}'.") { }
}
