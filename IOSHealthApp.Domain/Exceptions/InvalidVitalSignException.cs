namespace IOSHealthApp.Domain.Exceptions;

public class InvalidVitalSignException : DomainException
{
    public InvalidVitalSignException(string message) : base(message) { }
}
