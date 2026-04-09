using DigitalTwin.Mobile.Domain.Models;

namespace DigitalTwin.Mobile.Domain.Interfaces;

public interface IDrugSearchProvider
{
    Task<IEnumerable<DrugSearchResult>> SearchByNameAsync(string query, int maxResults = 8, CancellationToken ct = default);
}
