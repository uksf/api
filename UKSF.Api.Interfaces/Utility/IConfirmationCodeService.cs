﻿using System.Threading.Tasks;
using UKSF.Api.Interfaces.Data;

namespace UKSF.Api.Interfaces.Utility {
    public interface IConfirmationCodeService : IDataBackedService<IConfirmationCodeDataService> {
        Task<string> CreateConfirmationCode(string value, bool integration = false);
        Task<string> GetConfirmationCode(string id);
    }
}