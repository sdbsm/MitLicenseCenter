using Microsoft.Extensions.DependencyInjection;

namespace MitLicenseCenter.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Stage 1 — пустая точка регистрации. Use-cases/handler'ы добавятся в Stage 2.
        return services;
    }
}
