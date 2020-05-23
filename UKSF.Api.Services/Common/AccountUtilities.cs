using UKSF.Api.Models.Personnel;
using UKSF.Common;

namespace UKSF.Api.Services.Common {
    public static class AccountUtilities {
        public static ExtendedAccount ToExtendedAccount(this Account account) {
            ExtendedAccount extendedAccount = account.Copy<ExtendedAccount>();
            extendedAccount.password = null;
            return extendedAccount;
        }
    }
}
