using System.Threading.RateLimiting;
using AspNetCoreRateLimit;
using Hangfire;
using Mesa.WhatsApp.Application;
using Mesa.WhatsApp.Infrastructure;
using Mesa.WhatsApp.Infrastructure.Jobs;
using Mesa.WhatsApp.Infrastructure.Persistence.Seed;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, services, config) => config
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File("logs/mesa-.log", rollingInterval: RollingInterval.Day));

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new() { Title = "MESA WhatsApp Business API", Version = "v1" });
        c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Description = "JWT Authorization header using the Bearer scheme.",
            Name = "Authorization",
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
            Scheme = "bearer"
        });
        c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
        {
            {
                new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Reference = new Microsoft.OpenApi.Models.OpenApiReference
                    {
                        Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
    });

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AdminUi", policy =>
            policy.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin());
    });

    builder.Services.AddMemoryCache();
    builder.Services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("IpRateLimiting"));
    builder.Services.AddInMemoryRateLimiting();
    builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

    builder.Services.AddRateLimiter(options =>
    {
        options.AddPolicy("webhook", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 120,
                    Window = TimeSpan.FromMinutes(1)
                }));

        options.AddPolicy("auth", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 20,
                    Window = TimeSpan.FromMinutes(1)
                }));

        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    });

    builder.Services.AddHealthChecks();

    var app = builder.Build();

    // HTTPS redirection (skip in Development when using HTTP-only local testing)
    if (!app.Environment.IsDevelopment())
        app.UseHttpsRedirection();

    app.UseSerilogRequestLogging();
    app.UseCors("AdminUi");
    app.UseIpRateLimiting();
    app.UseRateLimiter();

    if (app.Environment.IsDevelopment() || app.Configuration.GetValue("Swagger:Enabled", true))
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapHealthChecks("/health");
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = [new HangfireDevAuthorizationFilter()]
    });

    await DbSeeder.SeedAsync(app.Services);

    // Recurring campaign ticks
    RecurringJob.AddOrUpdate<CampaignJobs>(
        "campaign-tick",
        j => j.TickRunningCampaignsAsync(),
        "*/1 * * * *"); // every minute

    RecurringJob.AddOrUpdate<CampaignJobs>(
        "campaign-quality",
        j => j.EvaluateQualityAsync(),
        "*/15 * * * *");

    RecurringJob.AddOrUpdate<CampaignJobs>(
        "campaign-followups",
        j => j.ProcessFollowUpsAsync(),
        "*/30 * * * *");

    Log.Information("MESA WhatsApp API starting");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }

internal sealed class HangfireDevAuthorizationFilter : Hangfire.Dashboard.IDashboardAuthorizationFilter
{
    public bool Authorize(Hangfire.Dashboard.DashboardContext context) => true;
}
