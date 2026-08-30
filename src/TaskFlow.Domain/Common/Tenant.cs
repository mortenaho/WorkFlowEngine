namespace TaskFlow.Domain;

public static class Tenant
{
    public const string Default = "default";

    public static string Normalize(string? id) => string.IsNullOrEmpty(id) ? Default : id;
}
