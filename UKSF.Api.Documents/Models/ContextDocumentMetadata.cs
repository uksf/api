using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using UKSF.Api.Base.Models;

namespace UKSF.Api.Documents.Models {
    public class ContextDocumentMetadata : MongoObject {
        public DateTime CreatedUtc;
        [BsonRepresentation(BsonType.ObjectId)] public string CreatorId;
        public DateTime LastUpdatedUtc;
        public string Name;
        public string Path;
        public DocumentPermissions Permissions;
    }

    public class DocumentMetadata : ContextDocumentMetadata {
        public bool CanEdit;
        public bool CanView;
    }

    public class DocumentPermissions {
        public List<DocumentPermissionsQueryBlock> QueryBlocks;
    }

    public class DocumentPermissionsQueryBlock {
        public string ConditionOperator;
        public string Operator;
        public string Parameter;
        public List<DocumentPermissionsQueryBlock> QueryBlocks;
        public string Value;
    }

    public static class DocumentPermissionsOperators {
        public const string CONDITION = nameof(CONDITION);
        public const string AND = nameof(AND);
        public const string OR = nameof(OR);
        public const string BLOCK = nameof(BLOCK);
    }

    public static class DocumentPermissionsParameters {
        public const string ID = nameof(ID);
        public const string UNIT = nameof(UNIT);
        public const string RANK = nameof(RANK);
        public const string COMMANDER = nameof(COMMANDER);
    }

    public static class DocumentPermissionsConditionOperators {
        public const string IN = nameof(IN);
        public const string EQ = nameof(EQ);
        public const string NE = nameof(NE);
        public const string GT = nameof(GT);
        public const string GE = nameof(GE);
        public const string LT = nameof(LT);
        public const string LE = nameof(LE);
    }

    public static class DocumentPermissionsRules {
        public static readonly List<string> VALID_EVEN_OPERATORS =
            new() { DocumentPermissionsOperators.AND, DocumentPermissionsOperators.OR };
        public static readonly List<string> VALID_ODD_OPERATORS =
            new() { DocumentPermissionsOperators.CONDITION, DocumentPermissionsOperators.BLOCK };

        public static readonly List<string> VALID_PARAMETERS =
            new() { DocumentPermissionsParameters.ID, DocumentPermissionsParameters.UNIT, DocumentPermissionsParameters.RANK, DocumentPermissionsParameters.COMMANDER };

        public static readonly Dictionary<string, List<string>> VALID_CONDITION_OPERATORS_FOR_PARAMETERS = new() {
            { DocumentPermissionsParameters.ID, new() { DocumentPermissionsConditionOperators.IN } },
            { DocumentPermissionsParameters.UNIT, new() { DocumentPermissionsConditionOperators.IN } }, {
                DocumentPermissionsParameters.RANK,
                new() {
                    DocumentPermissionsConditionOperators.EQ,
                    DocumentPermissionsConditionOperators.NE,
                    DocumentPermissionsConditionOperators.GT,
                    DocumentPermissionsConditionOperators.GE,
                    DocumentPermissionsConditionOperators.LT,
                    DocumentPermissionsConditionOperators.LE
                }
            },
            { DocumentPermissionsParameters.COMMANDER, new() { "NONE" } }
        };
    }
}
