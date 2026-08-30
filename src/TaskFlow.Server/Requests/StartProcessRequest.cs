namespace TaskFlow.Server;

public sealed class StartProcessRequest
{
    public string ProcessKey { get; set; } = "";
    public string Initiator { get; set; } = "";
    public Dictionary<string, object?>? Parameters { get; set; }
}
