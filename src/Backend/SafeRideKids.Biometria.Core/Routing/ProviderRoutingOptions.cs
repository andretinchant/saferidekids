using System.Collections.Generic;

namespace SafeRideKids.Biometria.Core.Routing;

/// <summary>
/// Configuração da política de roteamento de provedores biométricos.
/// Lida diretamente da variável de ambiente BIOMETRIA_PROVIDERS_ATIVOS (CSV).
/// </summary>
public sealed class ProviderRoutingOptions
{
    public const string SectionName = "ProviderRouting";

    /// <summary>Lista de providerIds ativos (na ordem do CSV). Setter mutável para bind via IOptions.</summary>
    public List<string> ActiveProviderIds { get; set; } = new()
    {
        "aws-rekognition",
        "unico-idcloud"
    };

    /// <summary>Provider default quando lista de ativos está vazia (fallback de segurança).</summary>
    public string DefaultProviderId { get; set; } = "aws-rekognition";
}
