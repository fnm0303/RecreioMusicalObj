using Microsoft.Extensions.Options;
using RecreioMusical.Application;
using RecreioMusical.Infrastructure;
using RecreioMusical.Infrastructure.Spotify;
using RecreioMusical.Web.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.Configure<AdminOptions>(builder.Configuration.GetSection(AdminOptions.SectionName));

builder.Services.AddControllersWithViews();
builder.Services.AddProblemDetails();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.MapStaticAssets();

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
