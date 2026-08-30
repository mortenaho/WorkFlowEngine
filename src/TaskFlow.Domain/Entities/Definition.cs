namespace TaskFlow.Domain;

public sealed class Definition
{
    public string Id { get; set; } = "";
    public string TenantId { get; set; } = "";
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public DateTime CreatedAt { get; set; }

    public Definition Clone() => new()
    {
        Id = Id,
        TenantId = TenantId,
        Key = Key,
        Name = Name,
        CreatedAt = CreatedAt,
    };
}
