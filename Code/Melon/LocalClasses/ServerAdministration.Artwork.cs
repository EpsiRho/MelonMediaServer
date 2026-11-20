using Melon.Interface;

namespace Melon.LocalClasses;

public sealed partial class ServerAdministration
{
    private ServerAdministration()
    {
        ArtworkService.Instance.Changed += message => Messages.Publish("artwork", message);
    }

    public ArtworkStatus GetArtwork() => ArtworkService.Instance.GetStatus();
    public void SetArtworkProviderEnabled(string id, bool enabled) => ArtworkService.Instance.SetEnabled(id, enabled);
    public void SetArtworkProviderOrder(string[] ids) => ArtworkService.Instance.SetOrder(ids);
    public void SetArtworkProviderKey(string id, string apiKey) => ArtworkService.Instance.SetKey(id, apiKey);
    public void SetArtworkFetchAfterScan(bool enabled) => ArtworkService.Instance.SetFetchAfterScan(enabled);
    public Task<string> TestArtworkProvider(string id, string artistName) => ArtworkService.Instance.TestAsync(id, artistName);

    public bool FetchArtistArtwork(bool force = false)
    {
        lock (databaseLock)
        {
            if (!connected) throw new InvalidOperationException("Database unavailable.");
            return ArtworkService.Instance.StartFetch(force);
        }
    }
}
