namespace IOSHealthApp.Domain.Exceptions;

public class HealthDataUnavailableException : DomainException
{
    public HealthDataUnavailableException(string message) : base(message) { }
}
