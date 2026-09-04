using System.ComponentModel.DataAnnotations;

namespace ScoreTracker.Catalog.Infrastructure.Entities
{
    /// <summary>
    ///     One row per <b>listed entry</b> — every avatar as every official page serves it, 412 rows
    ///     across 182 pictures and 170 avatars (docs/design/avatar-selection.md §3).
    ///     <para>
    ///         Storing only the deduped pictures was not enough, and the reason is worth keeping:
    ///         the catalog answers two questions, not one. <i>What can I pick?</i> is per picture.
    ///         <i>What am I already wearing?</i> is per url — and each mix mirrors the same picture
    ///         at its own path, so a table holding only the canonical url recognised barely a
    ///         quarter of live accounts. Prefix-rewriting cannot substitute: the two Phoenix
    ///         directories reuse ids for unrelated art, so swapping <c>/avatars/</c> for
    ///         <c>/avatars/p2/</c> turns Azura into Electra.
    ///     </para>
    ///     <para>
    ///         Seeded by migration and otherwise static. There is no refresh job: all three source
    ///         pages need a login, and the dedupe behind these rows is a pixel comparison, which
    ///         has no business running at request time.
    ///     </para>
    /// </summary>
    internal sealed class AvatarEntity
    {
        [Key] public int Id { get; set; }

        /// <summary>
        ///     The avatar. Not a foreign key — there is no parent row, because an avatar has no
        ///     property its pictures do not.
        /// </summary>
        public int GroupId { get; set; }

        /// <summary>
        ///     The distinct picture within that avatar. Rows sharing one are the same art served
        ///     at each mix's own mirror path; only 12 avatars own more than one picture, because
        ///     Phoenix's decorative frame and XX's lower resolution are not different pictures.
        ///     The row with the highest-priority mix supplies the url a pin stores.
        /// </summary>
        public int PictureId { get; set; }

        /// <summary>
        ///     The official name, in the site's own casing. <b>Deliberately not unique.</b> The
        ///     live pages ship <c>Electra</c> twice, plus <c>Hero</c>/<c>hero</c> and
        ///     <c>Miya</c>/<c>MIYA</c>, and every one of those is a genuinely different picture.
        ///     Identity is the row; the name is a label and a search key.
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required] [MaxLength(400)] public string ImageUrl { get; set; } = string.Empty;

        /// <summary>
        ///     The mix this row is the listing for, as <c>1 &lt;&lt; (int)MixEnum</c> (XX = 1,
        ///     Phoenix = 2, Phoenix 2 = 4). A single bit, since a row is one page's listing; a
        ///     picture's mixes are the union across its rows, and an avatar's the union across
        ///     its pictures.
        /// </summary>
        public int Mixes { get; set; }

        /// <summary>Alphabetical by name, assigned at seed time so the picker needs no sort.</summary>
        public int SortOrder { get; set; }
    }
}
