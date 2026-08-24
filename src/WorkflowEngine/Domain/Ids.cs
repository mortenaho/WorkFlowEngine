using System.Security.Cryptography;

namespace WorkflowEngine;

public static class Ids
{
    public static string New()
    {
        Span<byte> bytes = stackalloc byte[16];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
