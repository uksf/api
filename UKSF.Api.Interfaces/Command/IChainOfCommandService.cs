using System.Collections.Generic;
using UKSF.Api.Models.Command;
using UKSF.Api.Models.Units;

namespace UKSF.Api.Interfaces.Command {
    public interface IChainOfCommandService {
        HashSet<string> ResolveChain(ChainOfCommandMode mode, string recipient, Unit start, Unit target);
        bool InContextChainOfCommand(string id);
    }
}
