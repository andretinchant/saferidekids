using System;

namespace SafeRideKids.Biometria.Core.Domain;

// Strongly-typed IDs como record struct.
// Justificativa: evita confusão entre IDs de tipos diferentes em chamadas
// (ex.: passar um ChildId onde se esperava um FamilyId compila silenciosamente
// quando ambos são string/Guid). Record struct mantém zero-allocation overhead.

/// <summary>Identificador único de um tenant (escola, frota).</summary>
public readonly record struct TenantId(string Value)
{
    public static TenantId Parse(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("TenantId vazio.", nameof(value))
            : new TenantId(value);

    public override string ToString() => Value;
}

/// <summary>Identificador único de uma família (responsável).</summary>
public readonly record struct FamilyId(Guid Value)
{
    public static FamilyId New() => new(Guid.NewGuid());
    public static FamilyId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

/// <summary>Identificador único de uma criança.</summary>
public readonly record struct ChildId(Guid Value)
{
    public static ChildId New() => new(Guid.NewGuid());
    public static ChildId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

/// <summary>Identificador único de um consentimento LGPD.</summary>
public readonly record struct ConsentId(Guid Value)
{
    public static ConsentId New() => new(Guid.NewGuid());
    public static ConsentId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

/// <summary>Identificador único de uma rota de transporte.</summary>
public readonly record struct RouteId(Guid Value)
{
    public static RouteId New() => new(Guid.NewGuid());
    public static RouteId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

/// <summary>Identificador único de uma parada da rota.</summary>
public readonly record struct RouteStopId(Guid Value)
{
    public static RouteStopId New() => new(Guid.NewGuid());
    public static RouteStopId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

/// <summary>Identificador único de um check-in.</summary>
public readonly record struct CheckInId(Guid Value)
{
    public static CheckInId New() => new(Guid.NewGuid());
    public static CheckInId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

/// <summary>Identificador único de um enrollment biométrico.</summary>
public readonly record struct EnrollmentId(Guid Value)
{
    public static EnrollmentId New() => new(Guid.NewGuid());
    public static EnrollmentId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
