namespace UKSF.Api.Core.Models;

public class CommandRequestsDataset
{
    public IEnumerable<CommandRequestDataset> MyRequests { get; set; }
    public IEnumerable<CommandRequestDataset> OtherRequests { get; set; }
}

public class CommandRequestDataset
{
    public bool CanOverride { get; set; }
    public CommandRequest Data { get; set; }
    public IEnumerable<CommandRequestReviewDataset> Reviews { get; set; }
}

public class CommandRequestReviewDataset
{
    public string Id { get; set; }
    public string Name { get; set; }
    public ReviewState State { get; set; }
}
