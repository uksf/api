using UKSF.Api.Models.Personnel;
using UKSF.Common;

namespace UKSF.Api.Services.Common {
    public static class AccountUtilities {
        public static PublicAccount ToPublicAccount(this Account account) {
            PublicAccount publicAccount = account.Copy<PublicAccount>();
            publicAccount.password = null;
            return publicAccount;
        }
    }
}
