namespace ScoreTracker.Web.Dtos.Api
{
    public sealed class PhoenixRecordDto
    {
        public string? Plate { get; set; }
        public string? LetterGrade { get; set; }
        public int? Score { get; set; }
        public bool IsBroken { get; set; }
        public DateTimeOffset RecordedDate { get; set; }
        public ChartDto Chart { get; set; }
    }
}
