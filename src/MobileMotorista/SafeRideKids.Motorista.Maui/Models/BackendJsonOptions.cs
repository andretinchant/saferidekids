using System.Text.Json;
using System.Text.Json.Serialization;

namespace SafeRideKids.Motorista.Maui;

// Opcoes JSON compartilhadas entre Refit e desserializacao manual.
// Backend usa camelCase no contrato; alinhado a CONTRACTS Secao 6.
internal static class BackendJsonOptions
{
    public static readonly JsonSerializerOptions Default = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
}
