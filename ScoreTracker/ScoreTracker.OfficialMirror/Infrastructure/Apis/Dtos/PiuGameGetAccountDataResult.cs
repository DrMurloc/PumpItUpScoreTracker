using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.OfficialMirror.Infrastructure.Apis.Dtos
{
    internal sealed class PiuGameGetAccountDataResult
    {
        public Name AccountName { get; set; }
        public Uri ImageUrl { get; set; }
        public TitleEntry[] TitleEntries { get; set; } = Array.Empty<TitleEntry>();

        public sealed class TitleEntry
        {
            public string Name { get; set; }

            public bool Have { get; set; }
            public string ColClass { get; set; }
        }
    }
}
