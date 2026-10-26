using System.Text;
using Hangfire;
using Hangfire.MemoryStorage;
using Hangfire.SqlServer;
using Mesa.WhatsApp.Application.Common.Interfaces;
using Mesa.WhatsApp.Application.Options;
using Mesa.WhatsApp.Infrastructure.Jobs;
using Mesa.WhatsApp.Infrastructure.Persistence;
using Mesa.WhatsApp.Infrastructure.Services;
using Mesa.WhatsApp.Infrastructure.WhatsApp;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Mesa.WhatsApp.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<WhatsAppOptions>(configuration.GetSection(WhatsAppOptions.SectionName));
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<CampaignEngineOptions>(configuration.GetSection(CampaignEngineOptions.SectionName));

        // Bind from environment variables (WHATSAPP_*, JWT_*, etc.)
        services.PostConfigure<WhatsAppOptions>(opts =>
        {
            opts.BusinessAccountId = Env(configuration, "WHATSAPP_BUSINESS_ACCOUNT_ID", opts.BusinessAccountId);
            opts.PhoneNumberId = Env(configuration, "WHATSAPP_PHONE_NUMBER_ID", opts.PhoneNumberId);
            opts.AccessToken = Env(configuration, "WHATSAPP_ACCESS_TOKEN", opts.AccessToken);
            opts.AppSecret = Env(configuration, "WHATSAPP_APP_SECRET", opts.AppSecret);
            opts.VerifyToken = Env(configuration, "WHATSAPP_VERIFY_TOKEN", opts.VerifyToken);
            opts.GraphApiVersion = Env(configuration, "GRAPH_API_VERSION", opts.GraphApiVersion);
            opts.DefaultLanguage = Env(configuration, "DEFAULT_LANGUAGE", opts.DefaultLanguage);
        });

        services.PostConfigure<JwtOptions>(opts =>
        {
            opts.SigningKey = Env(configuration, "JWT_SIGNING_KEY", opts.SigningKey);
            opts.Issuer = Env(configuration, "JWT_ISSUER", opts.Issuer);
            opts.Audience = Env(configuration, "JWT_AUDIENCE", opts.Audience);
        });

        var provider = configuration.GetValue<string>("Database:Provider") ?? "Sqlite";
        var connectionString = configuration.GetConnectionString("Default")
            ?? "Data Source=mesa_whatsapp.db";

        services.AddDbContext<AppDbContext>(options =>
        {
            if (string.Equals(provider, "SqlServer", StringComparison.OrdinalIgnoreCase))
                options.UseSqlServer(connectionString);
            else
                options.UseSqlite(connectionString);
        });
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddHttpClient<IWhatsAppCloudClient, WhatsAppCloudClient>((sp, client) =>
        {
            var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<WhatsAppOptions>>().Value;
            client.BaseAddress = new Uri($"{opts.GraphApiBaseUrl.TrimEnd('/')}/");
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", opts.AccessToken);
            client.Timeout = TimeSpan.FromSeconds(60);
        });

        services.AddScoped<IWebhookSignatureValidator, WebhookSignatureValidator>();
        services.AddScoped<IWebhookProcessor, WebhookProcessor>();
        services.AddScoped<ILeadImportService, LeadImportService>();
        services.AddScoped<ICampaignEngine, CampaignEngine>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IInboxService, InboxService>();
        services.AddScoped<IDemoBookingService, DemoBookingService>();
        services.AddScoped<ITemplateSyncService, TemplateSyncService>();
        services.AddScoped<CampaignJobs>();

        var hangfireStorage = configuration.GetValue<string>("Hangfire:Storage") ?? "Memory";
        services.AddHangfire(config =>
        {
            config.SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings();

            if (string.Equals(hangfireStorage, "SqlServer", StringComparison.OrdinalIgnoreCase))
                config.UseSqlServerStorage(connectionString);
            else
                config.UseMemoryStorage();
        });
        services.AddHangfireServer();

        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
        var signingKey = Env(configuration, "JWT_SIGNING_KEY", jwt.SigningKey);
        if (string.IsNullOrWhiteSpace(signingKey))
            signingKey = "DEV_ONLY_CHANGE_ME_MESA_WHATSAPP_JWT_KEY_32+";

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = Env(configuration, "JWT_ISSUER", jwt.Issuer),
                    ValidAudience = Env(configuration, "JWT_AUDIENCE", jwt.Audience),
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                    RoleClaimType = "role"
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("Owner", p => p.RequireRole(nameof(Domain.Enums.UserRole.Owner)));
            options.AddPolicy("ManagerOrAbove", p => p.RequireRole(
                nameof(Domain.Enums.UserRole.Owner),
                nameof(Domain.Enums.UserRole.Manager)));
            options.AddPolicy("SalesOrAbove", p => p.RequireRole(
                nameof(Domain.Enums.UserRole.Owner),
                nameof(Domain.Enums.UserRole.Manager),
                nameof(Domain.Enums.UserRole.SalesAgent)));
            options.AddPolicy("AnyAuthenticated", p => p.RequireAuthenticatedUser());
        });

        return services;
    }

    private static string Env(IConfiguration config, string key, string fallback)
    {
        var value = Environment.GetEnvironmentVariable(key);
        if (!string.IsNullOrWhiteSpace(value))
            return value;
        value = config[key];
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }
}
