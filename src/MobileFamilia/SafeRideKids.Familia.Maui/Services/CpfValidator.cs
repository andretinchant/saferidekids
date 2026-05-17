namespace SafeRideKids.Familia.Maui.Services;

/// <summary>
/// Validação de CPF apenas pelos dígitos verificadores (algoritmo módulo 11).
/// Não consulta Receita Federal. Não persiste CPF.
/// </summary>
public static class CpfValidator
{
    public static string OnlyDigits(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        Span<char> buffer = stackalloc char[input.Length];
        var len = 0;
        foreach (var c in input)
        {
            if (c >= '0' && c <= '9') buffer[len++] = c;
        }
        return new string(buffer[..len]);
    }

    public static bool IsValid(string cpf)
    {
        var digits = OnlyDigits(cpf);
        if (digits.Length != 11) return false;

        // CPFs com todos dígitos iguais são inválidos (regra clássica)
        var allSame = true;
        for (var i = 1; i < digits.Length; i++)
        {
            if (digits[i] != digits[0]) { allSame = false; break; }
        }
        if (allSame) return false;

        // Primeiro dígito verificador
        var sum = 0;
        for (var i = 0; i < 9; i++)
        {
            sum += (digits[i] - '0') * (10 - i);
        }
        var firstCheck = (sum * 10) % 11;
        if (firstCheck == 10) firstCheck = 0;
        if (firstCheck != digits[9] - '0') return false;

        // Segundo dígito verificador
        sum = 0;
        for (var i = 0; i < 10; i++)
        {
            sum += (digits[i] - '0') * (11 - i);
        }
        var secondCheck = (sum * 10) % 11;
        if (secondCheck == 10) secondCheck = 0;
        return secondCheck == digits[10] - '0';
    }

    public static string Mask(string raw)
    {
        var d = OnlyDigits(raw);
        if (d.Length <= 3) return d;
        if (d.Length <= 6) return $"{d[..3]}.{d[3..]}";
        if (d.Length <= 9) return $"{d[..3]}.{d[3..6]}.{d[6..]}";
        if (d.Length <= 11) return $"{d[..3]}.{d[3..6]}.{d[6..9]}-{d[9..]}";
        return $"{d[..3]}.{d[3..6]}.{d[6..9]}-{d[9..11]}";
    }
}
