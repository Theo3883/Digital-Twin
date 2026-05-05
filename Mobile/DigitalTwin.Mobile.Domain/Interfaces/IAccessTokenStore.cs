namespace DigitalTwin.Mobile.Domain.Interfaces;

public interface IAccessTokenStore
{
    string? AccessToken { get; set; }
    void Clear();
}

