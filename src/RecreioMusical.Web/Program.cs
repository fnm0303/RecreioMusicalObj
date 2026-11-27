using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RecreioMusical.Application;
using RecreioMusical.Infrastructure;
using RecreioMusical.Infrastructure.Persistence;
using RecreioMusical.Infrastructure.Spotify;
using RecreioMusical.Web.Configuration;
using RecreioMusical.Web.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment.ContentRootPath);
builder.Services.AddHostedService<SqliteBackupService>();
builder.Services.Configure<AdminOptions>(builder.Configuration.GetSection(AdminOptions.SectionName));

builder.Services.AddControllersWithViews();
builder.Services.AddProblemDetails();

builder.Services.AddSingleton<AdminPinService>();
builder.Services.AddSingleton<PinAttemptLimiter>();

var app = builder.Build();

var forwardedHeaders = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};

// O App Service fica atrás de um proxy com IP variável. O ForwardLimit padrão (1)
// usa apenas a entrada mais à direita, acrescentada pela plataforma, e ignora
// valores enviados pelo cliente.
forwardedHeaders.KnownIPNetworks.Clear();
forwardedHeaders.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeaders);

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
    await next();
});

await using (var scope = app.Services.CreateAsyncScope())
{
    var database = scope.ServiceProvider.GetRequiredService<PlaylistDbContext>();
    await database.Database.MigrateAsync();
    app.Logger.LogInformation("Banco da playlist pronto (SQLite).");
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();

    // O health check do App Service pode chegar por HTTP interno; /health não
    // é redirecionado para HTTPS para que a sondagem receba 200.
    app.UseWhen(
        context => !context.Request.Path.StartsWithSegments("/health"),
        branch => branch.UseHttpsRedirection());
}

app.MapStaticAssets();

app.MapGet("/health", async (PlaylistDbContext database, CancellationToken cancellationToken) =>
{
    try
    {
        var canConnect = await database.Database.CanConnectAsync(cancellationToken);

        return canConnect
            ? Results.Ok(new { status = "ok" })
            : Results.Json(
                new { status = "unhealthy" },
                statusCode: StatusCodes.Status503ServiceUnavailable);
    }
    catch (Exception) when (!cancellationToken.IsCancellationRequested)
    {
        return Results.Json(
            new { status = "unhealthy" },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

var spotify = app.Services.GetRequiredService<IOptions<SpotifyOptions>>().Value;
if (!spotify.IsConfigured)
{
    app.Logger.LogWarning(
        "Spotify não configurado: defina Spotify:ClientId e Spotify:ClientSecret via user-secrets ou variáveis de ambiente.");
}

app.Run();

public partial class Program
{
}
