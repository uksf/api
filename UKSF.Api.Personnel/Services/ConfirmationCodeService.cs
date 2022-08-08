using Newtonsoft.Json;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.ScheduledActions;
using UKSF.Api.Shared.Services;

namespace UKSF.Api.Personnel.Services;

public interface IConfirmationCodeService
{
    Task<string> CreateConfirmationCode(string value);
    Task<string> GetConfirmationCodeValue(string id);
    Task ClearConfirmationCodes(Func<ConfirmationCode, bool> predicate);
}

public class ConfirmationCodeService : IConfirmationCodeService
{
    private readonly IClock _clock;
    private readonly IConfirmationCodeContext _confirmationCodeContext;
    private readonly ISchedulerService _schedulerService;

    public ConfirmationCodeService(IConfirmationCodeContext confirmationCodeContext, ISchedulerService schedulerService, IClock clock)
    {
        _confirmationCodeContext = confirmationCodeContext;
        _schedulerService = schedulerService;
        _clock = clock;
    }

    public async Task<string> CreateConfirmationCode(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            throw new ArgumentNullException(nameof(value), "Value for confirmation code cannot be null or empty");
        }

        ConfirmationCode code = new() { Value = value };
        await _confirmationCodeContext.Add(code);
        await _schedulerService.CreateAndScheduleJob(_clock.UtcNow().AddMinutes(30), TimeSpan.Zero, ActionDeleteExpiredConfirmationCode.ActionName, code.Id);
        return code.Id;
    }

    public async Task<string> GetConfirmationCodeValue(string id)
    {
        var confirmationCode = _confirmationCodeContext.GetSingle(id);
        if (confirmationCode == null)
        {
            return string.Empty;
        }

        await _confirmationCodeContext.Delete(confirmationCode);
        var actionParameters = JsonConvert.SerializeObject(new object[] { confirmationCode.Id });
        await _schedulerService.Cancel(x => x.ActionParameters == actionParameters);

        return confirmationCode.Value;
    }

    public async Task ClearConfirmationCodes(Func<ConfirmationCode, bool> predicate)
    {
        var codes = _confirmationCodeContext.Get(predicate);
        foreach (var confirmationCode in codes)
        {
            var actionParameters = JsonConvert.SerializeObject(new object[] { confirmationCode.Id });
            await _schedulerService.Cancel(x => x.ActionParameters == actionParameters);
        }

        await _confirmationCodeContext.DeleteMany(x => predicate(x));
    }
}
