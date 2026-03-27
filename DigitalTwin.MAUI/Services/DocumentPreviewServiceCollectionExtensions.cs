using Microsoft.Extensions.DependencyInjection;

namespace DigitalTwin.Services;

public static class DocumentPreviewServiceCollectionExtensions
{
    public static IServiceCollection AddDocumentPreviewService(
        this IServiceCollection services)
    {
        services.AddSingleton<IDocumentPreviewService, DocumentPreviewService>();
        return services;
    }
}

