using System.Collections.Generic;

namespace UKSF.Api.Command.Models
{
    public class CommandRequestsDataset
    {
        public IEnumerable<CommandRequestDataset> MyRequests;
        public IEnumerable<CommandRequestDataset> OtherRequests;
    }

    public class CommandRequestDataset
    {
        public bool CanOverride;
        public CommandRequest Data;
        public IEnumerable<CommandRequestReviewDataset> Reviews;
    }

    public class CommandRequestReviewDataset
    {
        public string Id;
        public string Name;
        public ReviewState State;
    }
}
