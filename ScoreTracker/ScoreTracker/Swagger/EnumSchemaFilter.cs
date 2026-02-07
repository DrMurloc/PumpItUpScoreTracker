using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ScoreTracker.Web.Swagger
{
    public class EnumSchemaFilter : ISchemaFilter
    {
        public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
        {
            if (!context.Type.IsEnum)
                return;

            // Swashbuckle v10: you must cast to the concrete type to mutate schema
            if (schema is not OpenApiSchema s)
                return;

            s.Type = JsonSchemaType.String;
            s.Format = null;

            s.Enum ??= new List<JsonNode>();
            s.Enum.Clear();

            foreach (var name in Enum.GetNames(context.Type))
                s.Enum.Add(JsonValue.Create(name)!);
        }
    }
}
