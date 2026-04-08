using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Interfaces.Providers;

public interface IDrugSearchProvider
{
    Task<IEnumerable<DrugSearchResult>> SearchByNameAsync(
        string query,
        int maxResults = 8,
        CancellationToken ct = default);
}
