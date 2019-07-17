using System;
using System.Threading.Tasks;
using MongoDB.Driver;
using Newtonsoft.Json;
using UKSFWebsite.Api.Models;
using UKSFWebsite.Api.Services.Abstraction;
using UKSFWebsite.Api.Services.Utility;

namespace UKSFWebsite.Api.Services.Data {
    public class ConfirmationCodeService : DataService<ConfirmationCode>, IConfirmationCodeService {
        private readonly ISchedulerService schedulerService;

        public ConfirmationCodeService(IMongoDatabase database, ISchedulerService schedulerService) : base(database, "confirmationCodes") => this.schedulerService = schedulerService;

        public async Task<string> CreateConfirmationCode(string value, bool steam = false) {
            ConfirmationCode code = new ConfirmationCode {value = value};
            await Add(code);
            await schedulerService.Create(DateTime.Now.AddMinutes(30), TimeSpan.Zero, steam ? ScheduledJobType.STEAM : ScheduledJobType.NORMAL, nameof(SchedulerActionHelper.DeleteExpiredConfirmationCode), code.id);
            return code.id;
        }

        public async Task<string> GetConfirmationCode(string id) {
            ConfirmationCode confirmationCode = GetSingle(x => x.id == id);
            if (confirmationCode == null) return string.Empty;
            await Delete(confirmationCode.id);
            string actionParameters = JsonConvert.SerializeObject(new object[] {confirmationCode.id});
            if (actionParameters != null) {
                await schedulerService.Cancel(x => x.actionParameters == actionParameters);
            }

            return confirmationCode.value;
        }
    }
}
