using System.Collections.Generic;
using UKSFWebsite.Api.Models;

namespace UKSFWebsite.Api.Services.Abstraction {
    public interface IChainOfCommandService {
        HashSet<string> ResolveChain(ChainOfCommandMode mode, string recipient, Unit start, Unit target);
        bool InContextChainOfCommand(string id);
    }
}
