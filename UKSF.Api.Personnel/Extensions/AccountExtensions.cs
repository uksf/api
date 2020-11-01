using UKSF.Api.Base.Extensions;
using UKSF.Api.Personnel.Models;

namespace UKSF.Api.Personnel.Extensions {
    public static class AccountExtensions {
        // TODO: Use automapper
        public static PublicAccount ToPublicAccount(this Account account) {
            PublicAccount publicAccount = account.Copy<PublicAccount>();
            publicAccount.password = null;
            return publicAccount;
        }
    }
}
