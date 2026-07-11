using Microsoft.Extensions.DependencyInjection;

namespace Quorid.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers application-layer services. A home for MediatR handlers and
    /// FluentValidation validators as the CQRS layer is built out.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        return services;
    }
}
