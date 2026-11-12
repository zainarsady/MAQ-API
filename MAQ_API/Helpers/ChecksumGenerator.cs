using System.Security.Cryptography;
using System.Text;

namespace MAQ_API.Helpers;

public static class ChecksumGenerator
{
    /// <summary>Herepay checksum: drop "checksum", sort by key (ordinal), join values with ",", HMAC-SHA256, lowercase hex.</summary>
    public static string Generate(IReadOnlyDictionary<string, string> data, string privateKey)
    {
        var payload = BuildPayload(data);
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(privateKey), Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static bool Verify(IReadOnlyDictionary<string, string> data, string? received, string privateKey)
    {
        if (string.IsNullOrEmpty(received)) return false;
        var expected = Encoding.UTF8.GetBytes(Generate(data, privateKey));
        var actual = Encoding.UTF8.GetBytes(received.ToLowerInvariant());
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    public static string BuildPayload(IReadOnlyDictionary<string, string> data) =>
        string.Join(",", data
            .Where(kvp => !string.Equals(kvp.Key, "checksum", StringComparison.OrdinalIgnoreCase))
            .OrderBy(kvp => kvp.Key, StringComparer.Ordinal)
            .Select(kvp => kvp.Value));
}
