using System;

namespace SafeRideKids.Biometria.Core.Security;

/// <summary>
/// Wrapper de payload cifrado por KMS (envelope encryption) usado para colunas BYTEA
/// (ex.: display_name_encrypted). Mantém ciphertext + keyId/version juntos para audit
/// e rotacionamento de chave sem perder rastreabilidade.
/// </summary>
/// <typeparam name="T">Tipo lógico do dado claro (apenas marker — payload é serializado para bytes pelo caller).</typeparam>
public sealed record KmsEncrypted<T>(
    byte[] Ciphertext,
    string KeyId,
    int KeyVersion)
{
    public static KmsEncrypted<T> FromBytes(byte[] ciphertext, string keyId, int keyVersion = 1)
    {
        ArgumentNullException.ThrowIfNull(ciphertext);
        if (string.IsNullOrWhiteSpace(keyId)) throw new ArgumentException("KeyId vazio.", nameof(keyId));
        return new KmsEncrypted<T>(ciphertext, keyId, keyVersion);
    }
}
