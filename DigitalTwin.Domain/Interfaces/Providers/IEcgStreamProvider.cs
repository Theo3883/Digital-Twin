using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Interfaces.Providers;

public interface IEcgStreamProvider
{
    IObservable<EcgFrame> GetEcgStream();

    Task ConnectAsync(string deviceUrl, CancellationToken cancellationToken = default);

    Task DisconnectAsync();
}
