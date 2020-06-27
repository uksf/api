using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Utility;
using UKSF.Api.Models.Utility;
using UKSF.Api.Services.Utility.ScheduledActions;

namespace UKSF.Api.Services.Utility {
    public class ConfirmationCodeService : DataBackedService<IConfirmationCodeDataService>, IConfirmationCodeService {
        private readonly ISchedulerService schedulerService;

        public ConfirmationCodeService(IConfirmationCodeDataService data, ISchedulerService schedulerService) : base(data) => this.schedulerService = schedulerService;

        public async Task<string> CreateConfirmationCode(string value) {
            if (string.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(value), "Value for confirmation code cannot be null or empty");
            ConfirmationCode code = new ConfirmationCode { value = value };
            await Data.Add(code);
            await schedulerService.CreateAndSchedule(DateTime.Now.AddMinutes(30), TimeSpan.Zero, DeleteExpiredConfirmationCodeAction.ACTION_NAME, code.id);
            return code.id;
        }

        public async Task<string> GetConfirmationCode(string id) {
            ConfirmationCode confirmationCode = Data.GetSingle(id);
            if (confirmationCode == null) return string.Empty;
            await Data.Delete(confirmationCode.id);
            string actionParameters = JsonConvert.SerializeObject(new object[] { confirmationCode.id });
            await schedulerService.Cancel(x => x.actionParameters == actionParameters);
            return confirmationCode.value;
        }

        public async Task ClearConfirmationCodes(Func<ConfirmationCode, bool> predicate) {
            await Data.DeleteMany(predicate);
        }
    }
}
