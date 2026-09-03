using System.ComponentModel.DataAnnotations;

namespace ScoreTracker.Catalog.Infrastructure.Entities
{
    /// <summary>
    ///     One row per distinct avatar <b>picture</b>, not per avatar: the official pages list the
    ///     same avatar once per mix, and most of those listings are the same art. Rows sharing a
    ///     <see cref="GroupId" /> are one avatar's alternate pictures — 182 rows across 170 groups,
    ///     of which only 12 groups hold more than one (docs/design/avatar-selection.md §3).
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
        ///     Groups this picture with the same avatar's other pictures. Not a foreign key —
        ///     there is no parent row, because an avatar has no property a picture does not.
        /// </summary>
        public int GroupId { get; set; }

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
        ///     Which mixes render <b>this</b> picture, as a bitmask of <c>1 &lt;&lt; (int)MixEnum</c>
        ///     (XX = 1, Phoenix = 2, Phoenix 2 = 4). An avatar's availability is the union across
        ///     its group, which is why the mask lives on the picture rather than the group.
        /// </summary>
        public int Mixes { get; set; }

        /// <summary>Alphabetical by name, assigned at seed time so the picker needs no sort.</summary>
        public int SortOrder { get; set; }
    }
}
