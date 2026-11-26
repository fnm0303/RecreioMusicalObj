using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using RecreioMusical.Application.Models;
using RecreioMusical.Application.UseCases;
using RecreioMusical.Infrastructure.Realtime;
using RecreioMusical.Web.Api;
using RecreioMusical.Web.Security;

namespace RecreioMusical.Web.Controllers;

[ApiController]
[Route("api/playlist")]
public sealed class PlaylistController : ControllerBase
{
    private const string PinHeaderName = "X-Admin-Pin";
    private const int HeartbeatSeconds = 15;

    private static readonly JsonSerializerOptions SseJsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly GetPlaylistHandler _getPlaylist;
    private readonly AddTrackHandler _addTrack;
    private readonly RemoveTrackHandler _removeTrack;
    private readonly ClearPlaylistHandler _clearPlaylist;
    private readonly PlaylistChangeHub _hub;
    private readonly AdminPinService _adminPin;
    private readonly PinAttemptLimiter _pinLimiter;

    public PlaylistController(
        GetPlaylistHandler getPlaylist,
        AddTrackHandler addTrack,
        RemoveTrackHandler removeTrack,
        ClearPlaylistHandler clearPlaylist,
        PlaylistChangeHub hub,
        AdminPinService adminPin,
        PinAttemptLimiter pinLimiter)
    {
        ArgumentNullException.ThrowIfNull(getPlaylist);
        ArgumentNullException.ThrowIfNull(addTrack);
        ArgumentNullException.ThrowIfNull(removeTrack);
        ArgumentNullException.ThrowIfNull(clearPlaylist);
        ArgumentNullException.ThrowIfNull(hub);
        ArgumentNullException.ThrowIfNull(adminPin);
        ArgumentNullException.ThrowIfNull(pinLimiter);

        _getPlaylist = getPlaylist;
        _addTrack = addTrack;
        _removeTrack = removeTrack;
        _clearPlaylist = clearPlaylist;
        _hub = hub;
        _adminPin = adminPin;
        _pinLimiter = pinLimiter;
    }

    /// <summary>Estado atual da playlist compartilhada.</summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var playlist = await _getPlaylist.HandleAsync(cancellationToken);
        return Ok(playlist);
    }

    /// <summary>Adiciona uma faixa à primeira vaga livre.</summary>
    [HttpPost]
    public async Task<IActionResult> Add(
        [FromBody] AddTrackRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return ApiProblems.Create(
                this,
                StatusCodes.Status400BadRequest,
                "invalid_request",
                "Envie trackId e addedBy no corpo da requisição.",
                "Requisição inválida");
        }

        var result = await _addTrack.HandleAsync(
            new AddTrackCommand(request.TrackId, request.AddedBy),
            cancellationToken);

        if (result.IsFailure)
        {
            return ApiProblems.FromResult(this, result);
        }

        var playlist = await _getPlaylist.HandleAsync(cancellationToken);
        return Created("/api/playlist", new AddTrackResponse(result.Value, playlist));
    }

    /// <summary>Remove uma música; exige o PIN administrativo.</summary>
    [HttpDelete("{slot:int}")]
    public async Task<IActionResult> Remove(int slot, CancellationToken cancellationToken)
    {
        var denied = AuthorizeAdmin();
        if (denied is not null)
        {
            return denied;
        }

        var result = await _removeTrack.HandleAsync(
            new RemoveTrackCommand(slot),
            cancellationToken);

        return result.IsFailure
            ? ApiProblems.FromResult(this, result)
            : NoContent();
    }

    /// <summary>Esvazia a playlist; exige o PIN administrativo.</summary>
    [HttpDelete]
    public async Task<IActionResult> Clear(CancellationToken cancellationToken)
    {
        var denied = AuthorizeAdmin();
        if (denied is not null)
        {
            return denied;
        }

        var result = await _clearPlaylist.HandleAsync(cancellationToken);

        return result.IsFailure
            ? ApiProblems.FromResult(this, result)
            : NoContent();
    }

    /// <summary>
    /// Fluxo SSE: envia o snapshot atual ao conectar e cada mudança depois disso.
    /// Um comentário periódico mantém a conexão viva atrás de proxies.
    /// </summary>
    [HttpGet("events")]
    public async Task Events(CancellationToken cancellationToken)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache, no-transform";
        Response.Headers["X-Accel-Buffering"] = "no";

        await Response.WriteAsync("retry: 3000\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);

        await using var subscription = _hub.Subscribe();
        Task<bool>? pendingRead = null;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                pendingRead ??= subscription.WaitToReadAsync(cancellationToken).AsTask();

                var heartbeat = Task.Delay(
                    TimeSpan.FromSeconds(HeartbeatSeconds),
                    cancellationToken);

                var completed = await Task.WhenAny(pendingRead, heartbeat);

                if (completed == heartbeat)
                {
                    await Response.WriteAsync(": ping\n\n", cancellationToken);
                    await Response.Body.FlushAsync(cancellationToken);
                    continue;
                }

                if (!await pendingRead)
                {
                    break;
                }

                pendingRead = null;

                while (subscription.TryRead(out var playlist))
                {
                    var json = JsonSerializer.Serialize(playlist, SseJsonOptions);
                    await Response.WriteAsync(
                        $"event: playlist\ndata: {json}\n\n",
                        cancellationToken);
                    await Response.Body.FlushAsync(cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Cliente desconectou; nada a fazer.
        }
        catch (IOException)
        {
            // Conexão encerrada durante a escrita.
        }
    }

    private IActionResult? AuthorizeAdmin()
    {
        if (!_adminPin.IsEnabled)
        {
            return ApiProblems.Create(
                this,
                StatusCodes.Status403Forbidden,
                "admin_disabled",
                "A administração está desativada. Defina Admin:Pin no servidor.",
                "Administração desativada");
        }

        var origin = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        if (_pinLimiter.IsBlocked(origin, out var retryAfter))
        {
            var seconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
            Response.Headers["Retry-After"] = seconds.ToString(CultureInfo.InvariantCulture);

            return ApiProblems.Create(
                this,
                StatusCodes.Status429TooManyRequests,
                "too_many_attempts",
                $"Muitas tentativas de PIN. Aguarde {seconds} segundos.",
                "Muitas tentativas");
        }

        var providedPin = Request.Headers[PinHeaderName].ToString();

        if (!_adminPin.Verify(providedPin))
        {
            _pinLimiter.RecordFailure(origin);

            return ApiProblems.Create(
                this,
                StatusCodes.Status401Unauthorized,
                "invalid_pin",
                "PIN incorreto.",
                "PIN inválido");
        }

        _pinLimiter.Reset(origin);
        return null;
    }
}
