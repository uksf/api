using UKSF.Api.Command.Models;
using UKSF.Api.Command.Services;
using UKSF.Api.Personnel.Services;

namespace UKSF.Api.Command.Mappers
{
    public interface ILoaMapper
    {
        Loa MapToLoa(DomainLoaWithAccount domainLoa);
    }

    public class LoaMapper : ILoaMapper
    {
        private readonly IChainOfCommandService _chainOfCommandService;
        private readonly IDisplayNameService _displayNameService;

        public LoaMapper(IDisplayNameService displayNameService, IChainOfCommandService chainOfCommandService)
        {
            _displayNameService = displayNameService;
            _chainOfCommandService = chainOfCommandService;
        }

        public Loa MapToLoa(DomainLoaWithAccount domainLoa)
        {
            var displayName = _displayNameService.GetDisplayName(domainLoa.Account);
            var inContextChainOfCommand = _chainOfCommandService.InContextChainOfCommand(domainLoa.Recipient);
            return new()
            {
                Id = domainLoa.Id,
                Submitted = domainLoa.Submitted,
                Start = domainLoa.Start,
                End = domainLoa.End,
                State = domainLoa.State,
                Emergency = domainLoa.Emergency,
                Late = domainLoa.Late,
                Reason = domainLoa.Reason,
                LongTerm = (domainLoa.End - domainLoa.Start).Days > 21,
                Name = displayName,
                InChainOfCommand = inContextChainOfCommand
            };
        }
    }
}
