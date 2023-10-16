using ScoreTracker.Domain.Enums;
using ScoreTracker.Web.Dtos.Api;
using Swashbuckle.AspNetCore.Filters;

namespace ScoreTracker.Web.Swagger
{
    public class RecordPhoenixScoreDtoExample : IExamplesProvider<RecordPhoenixScoreDto>
    {
        public RecordPhoenixScoreDto GetExamples()
        {
            return new RecordPhoenixScoreDto
            {
                Score = 987654,
                Plate = PhoenixPlate.RoughGame.ToString(),
                SongName = "Kugutsu",
                ChartLevel = 25,
                ChartType = ChartType.Single.ToString(),
                IsBroken = true
            };
        }
    }
}
