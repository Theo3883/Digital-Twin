namespace DigitalTwin.Domain.Exceptions;

public class DuplicateAssignmentException : DomainException
{
    public DuplicateAssignmentException(Guid doctorId, Guid patientId)
        : base($"Patient '{patientId}' is already assigned to doctor '{doctorId}'.") { }
}
