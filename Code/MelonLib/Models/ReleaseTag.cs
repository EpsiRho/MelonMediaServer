using System;
using System.Collections.Generic;
using System.Linq;

namespace Melon.Models
{
    /// <summary>
    /// A release tag of the form "Branch-Major.Minor.Build.Revision", e.g. "Beta-0.9.0.0" or "Stable-0.9.0.1".
    /// Each branch numbers its own releases, so versions are only ordered within a branch; comparing across branches
    /// only tells you whether a switch goes to an older number.
    /// Shared with MelonInstaller as a linked source file, so it must not depend on anything else in MelonLib.
    /// </summary>
    public sealed record ReleaseTag(string Branch, Version Version)
    {
        public const string DefaultBranch = "Stable";
        public const string FallbackBranch = "Beta";

        public string Tag => $"{Branch}-{Version}";
        public string Title => $"{Branch} {Version}";
        public override string ToString() => Title;

        /// <summary>
        /// The tag a GitHub release stands for. Uses the release's git tag when it's in the right shape, otherwise its
        /// title, so a release tagged "b0.9.0.1" but titled "Beta 0.9.0.1" still counts. Null when neither works.
        /// </summary>
        public static string? TagOf(string? tagName, string? releaseName) =>
            TryParse(tagName, out var fromTag) ? fromTag.Tag
            : TryParse(releaseName, out var fromName) ? fromName.Tag
            : null;

        /// <summary>
        /// True when <paramref name="assetName"/> is the bundle zip for <paramref name="rid"/> (like win-x64). The release
        /// workflow names it "Melon-win-x64.zip"; MelonInstaller's build mode adds the tag, "Melon-Beta-0.9.0.1-win-x64.zip".
        /// </summary>
        public static bool IsBundle(string assetName, string rid) =>
            assetName.Equals($"Melon-{rid}.zip", StringComparison.OrdinalIgnoreCase) ||
            (assetName.StartsWith("Melon-", StringComparison.OrdinalIgnoreCase) && assetName.EndsWith($"-{rid}.zip", StringComparison.OrdinalIgnoreCase));

        /// <summary>Reads "Beta-0.9.0.1", "Beta-v0.9.0.1" or a release title like "Beta 0.9.0.1".</summary>
        public static bool TryParse(string? tag, out ReleaseTag release)
        {
            release = null!;
            if (string.IsNullOrWhiteSpace(tag)) return false;
            tag = tag.Trim();
            var dash = tag.LastIndexOfAny(['-', ' ']);
            if (dash <= 0 || dash == tag.Length - 1) return false;
            var branch = tag[..dash];
            if (!branch.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '.')) return false;
            if (!Version.TryParse(tag[(dash + 1)..].TrimStart('v'), out var version) || version.Revision < 0) return false;
            release = new ReleaseTag(branch, version);
            return true;
        }

        /// <summary>Branch names found in <paramref name="tags"/>, Stable first, then Beta, then the rest by name.</summary>
        public static string[] Branches(IEnumerable<string> tags) =>
            tags.Select(t => TryParse(t, out var r) ? r.Branch : null).OfType<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(b => b.Equals(DefaultBranch, StringComparison.OrdinalIgnoreCase) ? 0
                    : b.Equals(FallbackBranch, StringComparison.OrdinalIgnoreCase) ? 1 : 2)
                .ThenBy(b => b, StringComparer.OrdinalIgnoreCase)
                .ToArray();

        /// <summary>The branch a new install should use: Stable when it has releases, otherwise the first branch found.</summary>
        public static string? PreferredBranch(IEnumerable<string> tags) => Branches(tags).FirstOrDefault();

        /// <summary>The newest release on <paramref name="branch"/>, or null.</summary>
        public static ReleaseTag? Latest(IEnumerable<string> tags, string branch) =>
            tags.Select(t => TryParse(t, out var r) ? r : null).OfType<ReleaseTag>()
                .Where(r => r.Branch.Equals(branch, StringComparison.OrdinalIgnoreCase))
                .MaxBy(r => r.Version);

        /// <summary>True when moving from <paramref name="current"/> to <paramref name="target"/> lowers the version number.</summary>
        public static bool IsDowngrade(ReleaseTag? current, ReleaseTag target) => current != null && target.Version < current.Version;
    }
}
