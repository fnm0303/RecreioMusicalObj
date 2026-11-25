using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RecreioMusical.Domain;
using RecreioMusical.Infrastructure.Spotify;
using RecreioMusical.Web.Models;

namespace RecreioMusical.Web.Controllers;

public sealed class HomeController : Controller
{
    private readonly SpotifyOptions _spotifyOptions;
    private readonly IWebHostEnvironment _environment;

    public HomeController(
        IOptions<SpotifyOptions> spotifyOptions,
        IWebHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(spotifyOptions);
        ArgumentNullException.ThrowIfNull(environment);

        _spotifyOptions = spotifyOptions.Value;
        _environment = environment;
    }

    public IActionResult Index()
    {
        var model = new HomeViewModel
        {
            PlaylistCapacity = PlaylistRules.Capacity,
            ShowDevelopmentNote = _environment.IsDevelopment(),
            ShowSpotifySetupHint = _environment.IsDevelopment() && !_spotifyOptions.IsConfigured
        };

        return View(model);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
        });
    }
}
