using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UKSF.Api.Base.Context;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.ScheduledActions;
using UKSF.Api.Shared.Services;

namespace UKSF.Api.Personnel.Services {
    public interface IConfirmationCodeService : IDataBackedService<IConfirmationCodeDataService> {
        Task<string> CreateConfirmationCode(string value);
        Task<string> GetConfirmationCode(string id);
        Task ClearConfirmationCodes(Func<ConfirmationCode, bool> predicate);
    }

    public class ConfirmationCodeService : DataBackedService<IConfirmationCodeDataService>, IConfirmationCodeService {
        private readonly ISchedulerService schedulerService;

        public ConfirmationCodeService(IConfirmationCodeDataService data, ISchedulerService schedulerService) : base(data) => this.schedulerService = schedulerService;

        public async Task<string> CreateConfirmationCode(string value) {
            if (string.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(value), "Value for confirmation code cannot be null or empty");
            ConfirmationCode code = new ConfirmationCode { value = value };
            await Data.Add(code);
            await schedulerService.CreateAndScheduleJob(DateTime.Now.AddMinutes(30), TimeSpan.Zero, ActionDeleteExpiredConfirmationCode.ACTION_NAME, code.id);
            return code.id;
        }

        public async Task<string> GetConfirmationCode(string id) {
            ConfirmationCode confirmationCode = Data.GetSingle(id);
            if (confirmationCode == null) return string.Empty;
            await Data.Delete(confirmationCode);
            string actionParameters = JsonConvert.SerializeObject(new object[] { confirmationCode.id });
            await schedulerService.Cancel(x => x.actionParameters == actionParameters);
            return confirmationCode.value;
        }

        public async Task ClearConfirmationCodes(Func<ConfirmationCode, bool> predicate) {
            IEnumerable<ConfirmationCode> codes = Data.Get(predicate);
            foreach (ConfirmationCode confirmationCode in codes) {
                string actionParameters = JsonConvert.SerializeObject(new object[] { confirmationCode.id });
                await schedulerService.Cancel(x => x.actionParameters == actionParameters);
            }
            await Data.DeleteMany(x => predicate(x));
        }
    }
}
