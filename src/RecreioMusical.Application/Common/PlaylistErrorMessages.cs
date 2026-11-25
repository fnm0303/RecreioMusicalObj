namespace RecreioMusical.Application.Common;

/// <summary>Mensagens exibidas ao usuário quando uma operação falha.</summary>
public static class PlaylistErrorMessages
{
    public const string InvalidTrackId = "Identificador de música inválido.";
    public const string InvalidSearchQuery = "Digite pelo menos 2 caracteres para buscar.";
    public const string SearchQueryTooLong = "A busca deve ter no máximo 80 caracteres.";
    public const string PlaylistFull = "A playlist já está completa com 5 músicas.";
    public const string PlaylistBusy = "Muita gente escolhendo ao mesmo tempo. Tente novamente.";
    public const string DuplicateTrack = "Essa música já está na playlist.";
    public const string TrackNotFound = "Música não encontrada no Spotify.";
    public const string SpotifyUnavailable = "O Spotify está indisponível no momento. Tente novamente em instantes.";
    public const string InvalidSlot = "Vaga inválida.";
    public const string ItemNotFound = "Essa música não está mais na playlist.";
}
