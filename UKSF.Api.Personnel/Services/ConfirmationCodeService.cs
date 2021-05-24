using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.ScheduledActions;
using UKSF.Api.Shared.Services;

namespace UKSF.Api.Personnel.Services
{
    public interface IConfirmationCodeService
    {
        Task<string> CreateConfirmationCode(string value);
        Task<string> GetConfirmationCodeValue(string id);
        Task ClearConfirmationCodes(Func<ConfirmationCode, bool> predicate);
    }

    public class ConfirmationCodeService : IConfirmationCodeService
    {
        private readonly IConfirmationCodeContext _confirmationCodeContext;
        private readonly ISchedulerService _schedulerService;

        public ConfirmationCodeService(IConfirmationCodeContext confirmationCodeContext, ISchedulerService schedulerService)
        {
            _confirmationCodeContext = confirmationCodeContext;
            _schedulerService = schedulerService;
        }

        public async Task<string> CreateConfirmationCode(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentNullException(nameof(value), "Value for confirmation code cannot be null or empty");
            }

            ConfirmationCode code = new() { Value = value };
            await _confirmationCodeContext.Add(code);
            await _schedulerService.CreateAndScheduleJob(DateTime.Now.AddMinutes(30), TimeSpan.Zero, ActionDeleteExpiredConfirmationCode.ACTION_NAME, code.Id);
            return code.Id;
        }

        public async Task<string> GetConfirmationCodeValue(string id)
        {
            ConfirmationCode confirmationCode = _confirmationCodeContext.GetSingle(id);
            if (confirmationCode == null)
            {
                return string.Empty;
            }

            await _confirmationCodeContext.Delete(confirmationCode);
            string actionParameters = JsonConvert.SerializeObject(new object[] { confirmationCode.Id });
            await _schedulerService.Cancel(x => x.ActionParameters == actionParameters);

            return confirmationCode.Value;
        }

        public async Task ClearConfirmationCodes(Func<ConfirmationCode, bool> predicate)
        {
            IEnumerable<ConfirmationCode> codes = _confirmationCodeContext.Get(predicate);
            foreach (ConfirmationCode confirmationCode in codes)
            {
                string actionParameters = JsonConvert.SerializeObject(new object[] { confirmationCode.Id });
                await _schedulerService.Cancel(x => x.ActionParameters == actionParameters);
            }

            await _confirmationCodeContext.DeleteMany(x => predicate(x));
        }
    }
}
