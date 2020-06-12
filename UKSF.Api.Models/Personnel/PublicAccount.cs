// ReSharper disable ClassNeverInstantiated.Global

namespace UKSF.Api.Models.Personnel {
    public class PublicAccount : Account {
        public string displayName;
        public AccountPermissions permissions = new AccountPermissions();
    }
}
