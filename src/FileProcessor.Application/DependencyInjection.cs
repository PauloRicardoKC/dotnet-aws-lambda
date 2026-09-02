using FileProcessor.Application.Abstractions;
using FileProcessor.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FileProcessor.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddFileProcessing(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddTransient<IFileProcessor, Services.FileProcessor>();

        return services;
    }
}
