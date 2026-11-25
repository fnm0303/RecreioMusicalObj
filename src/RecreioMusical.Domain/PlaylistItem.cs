namespace RecreioMusical.Domain;

/// <summary>
/// Uma música ocupando uma das cinco vagas da playlist.
/// </summary>
public sealed class PlaylistItem
{
    private PlaylistItem(int slot, TrackInfo track, Nickname addedBy, DateTimeOffset addedAt)
    {
        Slot = slot;
        Track = track;
        AddedBy = addedBy;
        AddedAt = addedAt;
    }

    public int Slot { get; }

    public TrackInfo Track { get; }

    public Nickname AddedBy { get; }

    public DateTimeOffset AddedAt { get; }

    public static PlaylistItem Place(
        int slot,
        TrackInfo track,
        Nickname addedBy,
        DateTimeOffset addedAt)
    {
        ArgumentNullException.ThrowIfNull(track);
        ArgumentNullException.ThrowIfNull(addedBy);

        if (!PlaylistRules.IsValidSlot(slot))
        {
            throw new DomainException(
                PlaylistErrors.InvalidSlot,
                $"A vaga deve estar entre {PlaylistRules.FirstSlot} e {PlaylistRules.LastSlot}.");
        }

        return new PlaylistItem(slot, track, addedBy, addedAt);
    }
}
