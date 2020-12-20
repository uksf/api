using UKSF.Api.Documents.Models;
using UKSF.Api.Shared;
using UKSF.Api.Shared.Services;

namespace UKSF.Api.Documents.Queries {
    public interface IUserPermissionsForDocumentQuery {
        public UserPermissionsForDocumentResult Execute(UserPermissionsForDocumentQueryArgs args);
    }

    public class UserPermissionsForDocumentQueryArgs {
        public UserPermissionsForDocumentQueryArgs(ContextDocumentMetadata contextDocument) => ContextDocument = contextDocument;

        public ContextDocumentMetadata ContextDocument { get; }
    }

    public class UserPermissionsForDocumentResult {
        public bool CanView { get; }
        public bool CanEdit { get; }

        public UserPermissionsForDocumentResult(bool canView, bool canEdit) {
            CanView = canView;
            CanEdit = canEdit;
        }
    }

    public class UserPermissionsForDocumentQuery : IUserPermissionsForDocumentQuery {
        private readonly IHttpContextService _httpContextService;

        public UserPermissionsForDocumentQuery(IHttpContextService httpContextService) => _httpContextService = httpContextService;

        public UserPermissionsForDocumentResult Execute(UserPermissionsForDocumentQueryArgs args) {
            if (_httpContextService.UserHasPermission(Permissions.ADMIN)) {
                return new (true, true);
            }

            string userId = _httpContextService.GetUserId();
            bool isCreator = args.ContextDocument.CreatorId == userId;
            return new (isCreator, isCreator);
        }
    }
}
