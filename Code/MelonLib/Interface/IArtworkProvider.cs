using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Melon.Interface
{
    /// <summary>The kinds of artist artwork Melon can fill in from an online provider.</summary>
    public enum ArtworkKind
    {
        /// <summary>A square artist profile picture.</summary>
        ArtistPicture,
        /// <summary>A wide artist header or background image.</summary>
        ArtistBanner
    }

    /// <summary>What Melon knows about the artist it is looking up.</summary>
    /// <param name="ArtistName">The artist name as it appears in the library.</param>
    /// <param name="MusicBrainzArtistId">The MusicBrainz artist ID, when Melon could resolve one.
    /// Melon only resolves it when an enabled provider sets <see cref="IArtworkProvider.RequiresMusicBrainzId"/>.</param>
    /// <param name="AlbumNames">Album names in the library for this artist, to tell apart artists that share a name.</param>
    public sealed record ArtworkQuery(string ArtistName, string? MusicBrainzArtistId, IReadOnlyList<string> AlbumNames);

    /// <summary>One image a provider offers. Melon downloads, validates and stores it.</summary>
    public sealed record ArtworkCandidate(Uri Url, ArtworkKind Kind, int? Width = null, int? Height = null);

    /// <summary>Per-call services and settings Melon passes to a provider.</summary>
    public sealed class ArtworkProviderContext
    {
        /// <summary>The API key the admin entered for this provider, or null.</summary>
        public string? ApiKey { get; init; }

        /// <summary>A shared client that already sends Melon's User-Agent. Do not dispose it.</summary>
        public required HttpClient Http { get; init; }
    }

    /// <summary>
    /// A source of artist artwork. Register it with <see cref="IHost.Artwork"/> in your plugin's
    /// <c>Execute()</c> and unregister it in <c>Destroy()</c>. Admins enable, order and configure providers
    /// from the controllers; Melon asks enabled providers in priority order and keeps the first image found
    /// for each <see cref="ArtworkKind"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// public int Execute() { Host.Artwork.Register(new MyProvider()); return 0; }
    /// public int Destroy() { Host.Artwork.Unregister("my-provider"); return 0; }
    /// </code>
    /// </example>
    public interface IArtworkProvider
    {
        /// <summary>A stable, lowercase key such as "deezer". Settings are saved under this key.</summary>
        string Id { get; }

        /// <summary>The name shown to admins.</summary>
        string Name { get; }

        /// <summary>A one-line description shown to admins.</summary>
        string Description { get; }

        /// <summary>The kinds of artwork this provider can return.</summary>
        IReadOnlyCollection<ArtworkKind> Kinds { get; }

        /// <summary>True when the provider cannot work until an admin enters an API key.</summary>
        bool RequiresApiKey => false;

        /// <summary>Where admins can get an API key.</summary>
        string? ApiKeyUrl => null;

        /// <summary>True when the provider needs <see cref="ArtworkQuery.MusicBrainzArtistId"/>.
        /// Melon looks it up on MusicBrainz and skips the provider for artists it cannot match.</summary>
        bool RequiresMusicBrainzId => false;

        /// <summary>Whether the provider starts enabled the first time Melon sees it.</summary>
        bool EnabledByDefault => false;

        /// <summary>
        /// Finds artwork for one artist. Return an empty list when nothing matches confidently; a wrong
        /// image is worse than none. Throw <see cref="UnauthorizedAccessException"/> when the API key is rejected.
        /// Providers own their rate limiting.
        /// </summary>
        Task<IReadOnlyList<ArtworkCandidate>> FindArtistArtworkAsync(ArtworkQuery query, ArtworkProviderContext context,
            CancellationToken cancellation);
    }

    /// <summary>The set of artwork providers Melon can use.</summary>
    public interface IArtworkProviderRegistry
    {
        /// <summary>Adds a provider. Throws if another provider already uses its <see cref="IArtworkProvider.Id"/>.</summary>
        void Register(IArtworkProvider provider);

        /// <summary>Removes a provider. Its saved settings are kept for when it comes back.</summary>
        bool Unregister(string id);

        /// <summary>All registered providers in priority order, including disabled ones.</summary>
        IReadOnlyList<IArtworkProvider> Providers { get; }
    }
}
