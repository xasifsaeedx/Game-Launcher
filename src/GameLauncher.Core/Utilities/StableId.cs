using System.Security.Cryptography;
using System.Text;

namespace GameLauncher.Core.Utilities;

public static class StableId
{
    public static Guid FromText(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value.Trim()));
        var bytes = hash[..16];

        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);

        return new Guid(bytes);
    }
}
