using Mesa.WhatsApp.Application.Common.Interfaces;
using Mesa.WhatsApp.Application.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace Mesa.WhatsApp.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<IContentRulesValidator, ContentRulesValidator>();
        return services;
    }
}
