namespace TaskFlow.Application;

public sealed class ProcessList
{
    public string ProcessKey { get; set; } = "";
    public int Total { get; set; }
    public List<ProcessInstanceDetail> Instances { get; set; } = [];
}
