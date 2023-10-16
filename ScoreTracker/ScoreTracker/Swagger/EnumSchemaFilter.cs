using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ScoreTracker.Web.Swagger
{
    public class EnumSchemaFilter : ISchemaFilter
    {
        public void Apply(OpenApiSchema modelParam, SchemaFilterContext contextParam)
        {
            if (!contextParam.Type.IsEnum) return;

            modelParam.Type = "string";
            modelParam.Enum.Clear();
            Enum.GetNames(contextParam.Type).ToList().ForEach(n => modelParam.Enum.Add(new OpenApiString(n)));
        }
    }
}
