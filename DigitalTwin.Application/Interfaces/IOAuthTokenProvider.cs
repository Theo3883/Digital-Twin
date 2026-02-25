namespace DigitalTwin.Application.Interfaces;

public interface IOAuthTokenProvider
{
    Task<OAuthTokenResult> GetTokensAsync();
}

