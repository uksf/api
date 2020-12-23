using System.Linq;
using UKSF.Api.Documents.Exceptions;
using UKSF.Api.Documents.Models;

namespace UKSF.Api.Documents.Commands {
    public interface IVerifyDocumentPermissionsCommand {
        void Execute(VerifyDocumentPermissionsCommandArgs args);
    }

    public class VerifyDocumentPermissionsCommandArgs {
        public VerifyDocumentPermissionsCommandArgs(ContextDocumentMetadata document, DocumentPermissions permissions) {
            Document = document;
            Permissions = permissions;
        }

        public ContextDocumentMetadata Document { get; }
        public DocumentPermissions Permissions { get; }
    }

    public class VerifyDocumentPermissionsCommand : IVerifyDocumentPermissionsCommand {
        public void Execute(VerifyDocumentPermissionsCommandArgs args) {
            if (args.Permissions.QueryBlocks == null || !args.Permissions.QueryBlocks.Any()) {
                return;
            }

            if (args.Permissions.QueryBlocks.Count % 2 == 0) {
                throw new UksfInvalidDocumentPermissionsException("Invalid number of query blocks. There should be an odd number of query blocks");
            }

            if (args.Permissions.QueryBlocks.Where((x, i) => i % 2 == 0).Any(x => x.Operator != DocumentPermissionsOperators.AND || x.Operator != DocumentPermissionsOperators.OR)) {
                throw new UksfInvalidDocumentPermissionsException("Invalid query block operators. Every even query block should be an operator (AND/OR)");
            }

            if (args.Permissions.QueryBlocks.Where((x, i) => i % 2 != 0).Any(x => x.Operator != DocumentPermissionsOperators.CONDITION || x.Operator != DocumentPermissionsOperators.BLOCK)) {
                throw new UksfInvalidDocumentPermissionsException("Invalid query block operators. Every even query block should be an operator (AND/OR)");
            }

            DocumentPermissionsQueryBlock invalidOperator = args.Permissions.QueryBlocks.FirstOrDefault(x => !DocumentPermissionsOperators.ValidOperators.Contains(x.Operator));
            if (invalidOperator != null) {
                throw new UksfInvalidDocumentPermissionsException($"Unrecognized query block operator. '{invalidOperator.Operator}' is not a valid operator (CONDITION/AND/OR/BLOCK)");
            }

            throw new UksfInvalidDocumentPermissionsException();
        }
    }
}
