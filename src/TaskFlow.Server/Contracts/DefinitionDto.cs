namespace TaskFlow.Server;

public sealed class DefinitionDto
{
    public string Id { get; set; } = "";
    public string TenantId { get; set; } = "";
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
