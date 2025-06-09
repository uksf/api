using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Mappers;

public interface ILoaMapper
{
    Loa MapToLoa(DomainLoaWithAccount loa);
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

    public Loa MapToLoa(DomainLoaWithAccount loa)
    {
        var displayName = _displayNameService.GetDisplayName(loa.Account);
        var inContextChainOfCommand = _chainOfCommandService.InContextChainOfCommand(loa.Recipient);
        return new Loa
        {
            Id = loa.Id,
            Submitted = loa.Submitted,
            Start = loa.Start,
            End = loa.End,
            State = loa.State,
            Emergency = loa.Emergency,
            Late = loa.Late,
            Reason = loa.Reason,
            LongTerm = (loa.End - loa.Start).Days > 21,
            Name = displayName,
            InChainOfCommand = inContextChainOfCommand
        };
    }
}
