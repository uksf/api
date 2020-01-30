using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Utility;
using UKSF.Api.Models.Utility;

namespace UKSF.Api.Services.Utility {
    public class ConfirmationCodeService : IConfirmationCodeService {
        private readonly IConfirmationCodeDataService data;
        private readonly ISchedulerService schedulerService;

        public ConfirmationCodeService(IConfirmationCodeDataService data, ISchedulerService schedulerService) {
            this.data = data;
            this.schedulerService = schedulerService;
        }

        public IConfirmationCodeDataService Data() => data;

        public async Task<string> CreateConfirmationCode(string value, bool integration = false) {
            ConfirmationCode code = new ConfirmationCode {value = value};
            await data.Add(code);
            await schedulerService.Create(DateTime.Now.AddMinutes(30), TimeSpan.Zero, integration ? ScheduledJobType.INTEGRATION : ScheduledJobType.NORMAL, nameof(SchedulerActionHelper.DeleteExpiredConfirmationCode), code.id);
            return code.id;
        }

        public async Task<string> GetConfirmationCode(string id) {
            ConfirmationCode confirmationCode = data.GetSingle(x => x.id == id);
            if (confirmationCode == null) return string.Empty;
            await data.Delete(confirmationCode.id);
            string actionParameters = JsonConvert.SerializeObject(new object[] {confirmationCode.id});
            if (actionParameters != null) {
                await schedulerService.Cancel(x => x.actionParameters == actionParameters);
            }

            return confirmationCode.value;
        }
    }
}
