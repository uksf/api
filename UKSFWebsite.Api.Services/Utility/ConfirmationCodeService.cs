using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UKSFWebsite.Api.Interfaces.Data;
using UKSFWebsite.Api.Interfaces.Utility;
using UKSFWebsite.Api.Models.Utility;

namespace UKSFWebsite.Api.Services.Utility {
    public class ConfirmationCodeService : DataBackedService<IConfirmationCodeDataService>, IConfirmationCodeService {
        private readonly ISchedulerService schedulerService;

        public ConfirmationCodeService(IConfirmationCodeDataService data, ISchedulerService schedulerService) : base(data) => this.schedulerService = schedulerService;

        public async Task<string> CreateConfirmationCode(string value, bool integration = false) {
            ConfirmationCode code = new ConfirmationCode {value = value};
            await Data().Add(code);
            await schedulerService.Create(DateTime.Now.AddMinutes(30), TimeSpan.Zero, integration ? ScheduledJobType.INTEGRATION : ScheduledJobType.NORMAL, nameof(SchedulerActionHelper.DeleteExpiredConfirmationCode), code.id);
            return code.id;
        }

        public async Task<string> GetConfirmationCode(string id) {
            ConfirmationCode confirmationCode = Data().GetSingle(x => x.id == id);
            if (confirmationCode == null) return string.Empty;
            await Data().Delete(confirmationCode.id);
            string actionParameters = JsonConvert.SerializeObject(new object[] {confirmationCode.id});
            if (actionParameters != null) {
                await schedulerService.Cancel(x => x.actionParameters == actionParameters);
            }

            return confirmationCode.value;
        }
    }
}
