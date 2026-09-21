using System.Security.Cryptography;
using System.Text;
using GameLauncher.Core.Security;

namespace GameLauncher.Infrastructure.Security;

public sealed class DpapiSecretProtector : ISecretProtector
{
    private static readonly byte[] Entropy =
        Encoding.UTF8.GetBytes("GameLauncher.HatchableSync.v1");

    public string Protect(string plaintext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plaintext);
        EnsureWindows();

        var bytes = Encoding.UTF8.GetBytes(plaintext);
        var protectedBytes = ProtectedData.Protect(
            bytes,
            Entropy,
            DataProtectionScope.CurrentUser);

        return Convert.ToBase64String(protectedBytes);
    }

    public string Unprotect(string protectedValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(protectedValue);
        EnsureWindows();

        var bytes = Convert.FromBase64String(protectedValue);
        var plaintext = ProtectedData.Unprotect(
            bytes,
            Entropy,
            DataProtectionScope.CurrentUser);

        return Encoding.UTF8.GetString(plaintext);
    }

    private static void EnsureWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Windows DPAPI is required to protect launcher sync credentials.");
        }
    }
}
