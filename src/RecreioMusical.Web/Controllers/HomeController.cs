using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RecreioMusical.Application.UseCases;
using RecreioMusical.Infrastructure.Spotify;
using RecreioMusical.Web.Configuration;
using RecreioMusical.Web.Models;

namespace RecreioMusical.Web.Controllers;

public sealed class HomeController : Controller
{
    private readonly GetPlaylistHandler _getPlaylist;
    private readonly SpotifyOptions _spotifyOptions;
    private readonly AdminOptions _adminOptions;
    private readonly IWebHostEnvironment _environment;

    public HomeController(
        GetPlaylistHandler getPlaylist,
        IOptions<SpotifyOptions> spotifyOptions,
        IOptions<AdminOptions> adminOptions,
        IWebHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(getPlaylist);
        ArgumentNullException.ThrowIfNull(spotifyOptions);
        ArgumentNullException.ThrowIfNull(adminOptions);
        ArgumentNullException.ThrowIfNull(environment);

        _getPlaylist = getPlaylist;
        _spotifyOptions = spotifyOptions.Value;
        _adminOptions = adminOptions.Value;
        _environment = environment;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var playlist = await _getPlaylist.HandleAsync(cancellationToken);

        var model = new HomeViewModel
        {
            Playlist = playlist,
            PlaylistCapacity = playlist.Capacity,
            SearchEnabled = _spotifyOptions.IsConfigured,
            AdminEnabled = _adminOptions.IsEnabled,
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
