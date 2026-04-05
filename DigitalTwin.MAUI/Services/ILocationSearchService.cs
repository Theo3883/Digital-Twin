using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DigitalTwin.Services;

public record LocationResult(string DisplayName, string City, string Country);

public interface ILocationSearchService
{
    Task<IReadOnlyList<LocationResult>> SearchAsync(string query, CancellationToken ct = default);
}
