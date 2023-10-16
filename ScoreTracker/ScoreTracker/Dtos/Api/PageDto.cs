namespace ScoreTracker.Web.Dtos.Api
{
    public sealed class PageDto<T>
    {
        public int Page { get; set; }
        public int Count { get; set; }
        public int TotalResults { get; set; }
        public T[] Results { get; set; }
    }
}
