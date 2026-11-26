using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using RecreioMusical.Web.Configuration;

namespace RecreioMusical.Web.Security;

/// <summary>
/// Verifica o PIN administrativo em tempo constante. O PIN em texto puro fica apenas
/// na configuração do servidor; aqui guardamos somente o hash.
/// </summary>
public sealed class AdminPinService
{
    private readonly byte[]? _pinHash;

    public AdminPinService(IOptions<AdminOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var pin = options.Value.Pin;
        if (options.Value.IsEnabled && !string.IsNullOrEmpty(pin))
        {
            _pinHash = SHA256.HashData(Encoding.UTF8.GetBytes(pin));
        }
    }

    public bool IsEnabled => _pinHash is not null;

    public bool Verify(string? candidate)
    {
        if (_pinHash is null || string.IsNullOrEmpty(candidate))
        {
            return false;
        }

        var candidateHash = SHA256.HashData(Encoding.UTF8.GetBytes(candidate));
        return CryptographicOperations.FixedTimeEquals(_pinHash, candidateHash);
    }
}
