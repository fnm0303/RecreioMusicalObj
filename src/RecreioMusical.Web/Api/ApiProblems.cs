using Microsoft.AspNetCore.Mvc;
using RecreioMusical.Application.Common;
using RecreioMusical.Domain;

namespace RecreioMusical.Web.Api;

/// <summary>Respostas de erro da API no formato Problem Details, com código estável.</summary>
public static class ApiProblems
{
    public static ObjectResult FromResult(ControllerBase controller, Result result)
    {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(result);

        var error = result.Error ?? new ResultError("unknown", "Não foi possível concluir.");
        var (statusCode, title) = Map(error.Code);

        return Create(controller, statusCode, error.Code, error.Message, title);
    }

    public static ObjectResult Create(
        ControllerBase controller,
        int statusCode,
        string code,
        string detail,
        string? title = null)
    {
        ArgumentNullException.ThrowIfNull(controller);

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title ?? "Não foi possível concluir",
            Detail = detail,
            Instance = controller.Request.Path
        };

        problem.Extensions["code"] = code;

        return new ObjectResult(problem)
        {
            StatusCode = statusCode,
            ContentTypes = { "application/problem+json" }
        };
    }

    private static (int StatusCode, string Title) Map(string code) => code switch
    {
        ApplicationErrors.InvalidSearchQuery => (400, "Busca inválida"),
        ApplicationErrors.InvalidTrackId => (400, "Música inválida"),
        PlaylistErrors.InvalidNickname => (400, "Apelido inválido"),
        PlaylistErrors.InvalidSlot => (400, "Vaga inválida"),
        ApplicationErrors.TrackNotFound => (404, "Música não encontrada"),
        ApplicationErrors.ItemNotFound => (404, "Música fora da playlist"),
        ApplicationErrors.PlaylistFull => (409, "Playlist completa"),
        ApplicationErrors.DuplicateTrack => (409, "Música repetida"),
        ApplicationErrors.PlaylistBusy => (409, "Playlist movimentada"),
        ApplicationErrors.SpotifyUnavailable => (503, "Spotify indisponível"),
        _ => (400, "Não foi possível concluir")
    };
}
