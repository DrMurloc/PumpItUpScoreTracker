namespace ScoreTracker.Web.Shared
{
    public static class RankingColors
    {
        public static string ColorStyle(double? ranking)
        {
            return ranking == null ? "" :
                ranking <= .1 ? "color:#BDBDBD;" :
                ranking <= .25 ? "color:#FAFAFA;" :
                ranking <= .5 ? "color:#76FF03;" :
                ranking <= .75 ? "color:#1565C0;" :
                ranking <= .9 ? "color:#7E57C2;" :
                ranking <= .99 ? "color:#EC407A;" :
                "color:#FB8C00;";
        }
    }
}
