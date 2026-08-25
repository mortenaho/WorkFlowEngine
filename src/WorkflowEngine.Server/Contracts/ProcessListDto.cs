namespace WorkflowEngine.Server;

public sealed class ProcessListDto
{
    public string ProcessKey { get; set; } = "";
    public int Total { get; set; }
    public List<ProcessInstanceDetailDto> Instances { get; set; } = [];
}
