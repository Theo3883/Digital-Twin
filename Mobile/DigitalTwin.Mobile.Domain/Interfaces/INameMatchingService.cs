using DigitalTwin.Mobile.Domain.Models;

namespace DigitalTwin.Mobile.Domain.Interfaces;

public interface INameMatchingService
{
    NameMatchResult Match(string expected, string actual);
}
