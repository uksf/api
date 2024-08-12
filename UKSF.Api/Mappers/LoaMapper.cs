using UKSF.Api.Core.Models;
using UKSF.Api.Core.Services;
using UKSF.Api.Services;

namespace UKSF.Api.Mappers;

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
        return new Loa
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
