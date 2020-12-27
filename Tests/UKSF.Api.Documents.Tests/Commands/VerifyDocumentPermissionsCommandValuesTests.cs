using System;
using FluentAssertions;
using UKSF.Api.Documents.Commands;
using UKSF.Api.Documents.Exceptions;
using UKSF.Api.Documents.Models;
using Xunit;

namespace UKSF.Api.Documents.Tests.Commands {
    public class VerifyDocumentPermissionsCommandValuesTests {
        private readonly IVerifyDocumentPermissionsCommand _subject;

        public VerifyDocumentPermissionsCommandValuesTests() => _subject = new VerifyDocumentPermissionsCommand();

        [Fact]
        public void When_validating_document_permissions_values_with_id_value_invalid_object_id() {
            void Act() =>
                _subject.Execute(
                    new(
                        new(),
                        new() {
                            QueryBlocks = new() {
                                new() { Operator = DocumentPermissionsOperators.CONDITION, Parameter = DocumentPermissionsParameters.ID, ConditionOperator = DocumentPermissionsConditionOperators.IN, Value = "[\"invalidobjectid\"]"}
                            }
                        }
                    )
                );

            Should_throw_invalid_exception_with_message(Act, "Invalid query block parameter value for parameter 'ID'. Parameter value for 'ID' must be a valid list of account IDs");
        }

        private static void Should_throw_invalid_exception_with_message(Action act, string message) {
            act.Should().Throw<UksfInvalidDocumentPermissionsException>().WithMessage(message).And.StatusCode.Should().Be(400);
        }
    }
}
