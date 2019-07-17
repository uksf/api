using System.Collections.Generic;
using UKSFWebsite.Api.Models;

namespace UKSFWebsite.Api.Services.Abstraction {
    public interface IChainOfCommandService {
        HashSet<string> ResolveChain(ChainOfCommandMode mode, Unit start, Unit target = null);
        bool InContextChainOfCommand(string id);
    }
}
