using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Catalog.Contracts;

/// <summary>
///     One avatar a player may wear, with every picture the official pages draw it as.
///     <para>
///         <paramref name="Name" /> is <b>not unique</b> across the catalog — the official pages
///         ship colliding names for genuinely different avatars — so never key off it. Order in
///         the catalog is stable and alphabetical; use position or <paramref name="Pictures" />
///         to tell two same-named entries apart.
///     </para>
/// </summary>
/// <param name="Name">The official name, in the site's own casing.</param>
/// <param name="Mixes">Every mix that lists this avatar, ascending.</param>
/// <param name="Pictures">
///     The distinct art for this avatar, best first. All but twelve avatars have exactly one:
///     Phoenix's decorative frame is not a different picture, and neither is XX's lower
///     resolution (docs/design/avatar-selection.md §3).
/// </param>
[ExcludeFromCodeCoverage]
public sealed record AvatarRecord(
    string Name,
    IReadOnlyList<MixEnum> Mixes,
    IReadOnlyList<AvatarPictureRecord> Pictures);

/// <summary>
///     One picture, and every url it is served at.
///     <para>
///         The two are separate because each mix mirrors the same art under its own prefix, and a
///         player wears whichever one their mix's importer wrote. Recognising "you already have
///         this" therefore has to consider <paramref name="AllUrls" />, while a pin always stores
///         <paramref name="ImageUrl" />.
///     </para>
/// </summary>
/// <param name="ImageUrl">The canonical url, from the highest-priority mix that lists it. This is what a pin stores.</param>
/// <param name="Mixes">The mixes that render this particular picture.</param>
/// <param name="AllUrls">Every mirror path for this picture, including <paramref name="ImageUrl" />.</param>
[ExcludeFromCodeCoverage]
public sealed record AvatarPictureRecord(
    Uri ImageUrl,
    IReadOnlyList<MixEnum> Mixes,
    IReadOnlyList<Uri> AllUrls);
