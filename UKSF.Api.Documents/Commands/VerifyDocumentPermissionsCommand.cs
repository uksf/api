using System.Collections.Generic;
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

            ValidateOperators(args.Permissions.QueryBlocks);
            ValidateParameters(args.Permissions.QueryBlocks);
            ValidateConditionParameters(args.Permissions.QueryBlocks);

            throw new UksfInvalidDocumentPermissionsException();
        }

        private static void ValidateOperators(IReadOnlyCollection<DocumentPermissionsQueryBlock> queryBlocks) {
            if (queryBlocks.Where((x, i) => i % 2 != 0).Any(x => x.Operator != DocumentPermissionsOperators.AND && x.Operator != DocumentPermissionsOperators.OR)) {
                throw new UksfInvalidDocumentPermissionsException($"Invalid query block operators. Valid even operators are '{FormatOptions(DocumentPermissionsRules.VALID_EVEN_OPERATORS)}'");
            }

            if (queryBlocks.Where((x, i) => i % 2 == 0).Any(x => x.Operator != DocumentPermissionsOperators.CONDITION && x.Operator != DocumentPermissionsOperators.BLOCK)) {
                throw new UksfInvalidDocumentPermissionsException($"Invalid query block operators. Valid odd operators are '{FormatOptions(DocumentPermissionsRules.VALID_ODD_OPERATORS)}'");
            }

            foreach (DocumentPermissionsQueryBlock permissionsQueryBlocks in GetBlockQueryBlocks(queryBlocks)) {
                ValidateOperators(permissionsQueryBlocks.QueryBlocks);
            }
        }

        private static void ValidateParameters(IReadOnlyCollection<DocumentPermissionsQueryBlock> queryBlocks) {
            DocumentPermissionsQueryBlock invalidParameter = GetConditionQueryBlocks(queryBlocks).FirstOrDefault(x => !DocumentPermissionsRules.VALID_PARAMETERS.Contains(x.Parameter));
            if (invalidParameter != null) {
                throw new UksfInvalidDocumentPermissionsException(
                    $"Invalid query block parameters. '{invalidParameter.Parameter}' is not a valid parameter. Valid parameters are '{FormatOptions(DocumentPermissionsRules.VALID_PARAMETERS)}'"
                );
            }

            foreach (DocumentPermissionsQueryBlock permissionsQueryBlocks in GetBlockQueryBlocks(queryBlocks)) {
                ValidateParameters(permissionsQueryBlocks.QueryBlocks);
            }
        }

        private static void ValidateConditionParameters(IReadOnlyCollection<DocumentPermissionsQueryBlock> queryBlocks) {
            foreach (DocumentPermissionsQueryBlock queryBlock in GetConditionQueryBlocks(queryBlocks)) {
                List<string> validConditionOperators = DocumentPermissionsRules.VALID_CONDITION_OPERATORS_FOR_PARAMETERS[queryBlock.Parameter];
                if (!validConditionOperators.Contains(queryBlock.ConditionOperator)) {
                    throw new UksfInvalidDocumentPermissionsException(
                        $"Invalid query block condition operator for parameter '{queryBlock.Parameter}'. '{queryBlock.ConditionOperator}' is not a valid condition parameter. Valid condition parameters are '{FormatOptions(validConditionOperators)}'"
                    );
                }
            }

            foreach (DocumentPermissionsQueryBlock permissionsQueryBlocks in GetBlockQueryBlocks(queryBlocks)) {
                ValidateConditionParameters(permissionsQueryBlocks.QueryBlocks);
            }
        }

        private void ValidateParameterValues(IReadOnlyCollection<DocumentPermissionsQueryBlock> queryBlocks) {
            foreach (DocumentPermissionsQueryBlock queryBlock in GetConditionQueryBlocks(queryBlocks)) {

            }

            foreach (DocumentPermissionsQueryBlock permissionsQueryBlocks in GetBlockQueryBlocks(queryBlocks)) {
                ValidateConditionParameters(permissionsQueryBlocks.QueryBlocks);
            }
        }

        private static IEnumerable<DocumentPermissionsQueryBlock> GetConditionQueryBlocks(IEnumerable<DocumentPermissionsQueryBlock> queryBlocks) =>
            queryBlocks.Where(x => x.Operator == DocumentPermissionsOperators.CONDITION);

        private static IEnumerable<DocumentPermissionsQueryBlock> GetBlockQueryBlocks(IEnumerable<DocumentPermissionsQueryBlock> queryBlocks) =>
            queryBlocks.Where(x => x.Operator == DocumentPermissionsOperators.BLOCK);

        private static string FormatOptions(IReadOnlyCollection<string> options) => string.Join('/', options);
    }
}
