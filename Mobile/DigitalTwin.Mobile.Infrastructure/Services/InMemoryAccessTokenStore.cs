using DigitalTwin.Mobile.Domain.Interfaces;

namespace DigitalTwin.Mobile.Infrastructure.Services;

public sealed class InMemoryAccessTokenStore : IAccessTokenStore
{
    public string? AccessToken { get; set; }
    public void Clear() => AccessToken = null;
}

