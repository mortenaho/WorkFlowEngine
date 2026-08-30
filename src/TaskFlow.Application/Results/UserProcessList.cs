namespace TaskFlow.Application;

public sealed class UserProcessList
{
    public string User { get; set; } = "";
    public string State { get; set; } = "";
    public int Open { get; set; }
    public int Closed { get; set; }
    public int NotStarted { get; set; }
    public int Total { get; set; }
    public List<ProcessInstanceDetail> Instances { get; set; } = [];
}
