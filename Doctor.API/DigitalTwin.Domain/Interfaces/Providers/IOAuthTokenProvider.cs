namespace DigitalTwin.Domain.Interfaces.Providers;

public interface IOAuthTokenProvider
{
    Task<OAuthTokenResult> GetTokensAsync();
}
