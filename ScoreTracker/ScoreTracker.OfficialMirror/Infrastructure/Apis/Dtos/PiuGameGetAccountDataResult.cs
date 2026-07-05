using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.OfficialMirror.Infrastructure.Apis.Dtos
{
    internal sealed class PiuGameGetAccountDataResult
    {
        public Name AccountName { get; set; }
        public Uri ImageUrl { get; set; }
        public TitleEntry[] TitleEntries { get; set; } = Array.Empty<TitleEntry>();

        /// <summary>
        ///     True when the "INVALID" sentinel came from the site serving its login page —
        ///     i.e. the session isn't authenticated at all (wrong credentials). False for an
        ///     authenticated my_page that simply carries no game profile/card (the Phoenix 2
        ///     launch-week state). Lets OfficialSiteClient report the two truths distinctly.
        /// </summary>
        public bool RequiresLogin { get; set; }

        public sealed class TitleEntry
        {
            public string Name { get; set; }

            public bool Have { get; set; }
            public string ColClass { get; set; }
        }
    }
}
