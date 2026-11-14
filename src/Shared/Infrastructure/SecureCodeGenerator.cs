using System.Security.Cryptography;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Shared.Infrastructure;

/// <summary>P15 / K-24: <see cref="RandomNumberGenerator"/> tabanlı kod üreteci (N-20).</summary>
public sealed class SecureCodeGenerator : ISecureCodeGenerator
{
    /// <inheritdoc />
    public string NewCode(int length = SecureCode.DefaultLength)
    {
        if (length < SecureCode.MinLength)
        {
            throw new ArgumentOutOfRangeException(nameof(length), length, $"Kod en az {SecureCode.MinLength} karakter olmalıdır.");
        }

        return string.Create(length, 0, static (buffer, _) =>
        {
            for (var i = 0; i < buffer.Length; i++)
            {
                buffer[i] = SecureCode.Alphabet[RandomNumberGenerator.GetInt32(SecureCode.Alphabet.Length)];
            }
        });
    }
}
