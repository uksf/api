using System.Collections.Generic;
using UKSFWebsite.Api.Models.Command;
using UKSFWebsite.Api.Models.Units;

namespace UKSFWebsite.Api.Interfaces.Command {
    public interface IChainOfCommandService {
        HashSet<string> ResolveChain(ChainOfCommandMode mode, string recipient, Unit start, Unit target);
        bool InContextChainOfCommand(string id);
    }
}
