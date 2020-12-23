using System;
using FluentAssertions;
using UKSF.Api.Documents.Commands;
using UKSF.Api.Documents.Exceptions;
using UKSF.Api.Documents.Models;
using Xunit;

namespace UKSF.Api.Documents.Tests.Commands {
    public class VerifyDocumentPermissionsCommandTests {
        private readonly IVerifyDocumentPermissionsCommand _subject;

        public VerifyDocumentPermissionsCommandTests() => _subject = new VerifyDocumentPermissionsCommand();

        [Fact]
        public void When_validating_document_permissions_fails() {
            void Act() => _subject.Execute(new(new(), new()));

            Should_throw_invalid_exception_with_message(Act, "Invalid document permissions object");
        }

        [Fact]
        public void When_validating_document_permissions_with_null_query_blocks() {
            _subject.Execute(new(new(), new()));
        }

        [Fact]
        public void When_validating_document_permissions_with_empty_query_blocks() {
            _subject.Execute(new(new(), new() { QueryBlocks = new() }));
        }

        [Fact]
        public void When_validating_document_permissions_with_invalid_number_of_query_blocks() {
            void Act() => _subject.Execute(new(new(), new() { QueryBlocks = new() { new(), new() } }));

            Should_throw_invalid_exception_with_message(Act, "Invalid number of query blocks. There should be an odd number of query blocks");
        }

        [Fact]
        public void When_validating_document_permissions_with_any_second_query_block_not_an_operator() {
            void Act() => _subject.Execute(new(new(), new() { QueryBlocks = new() { new() {
                Operator = DocumentPermissionsOperators.CONDITION
            }, new() {
                Operator = DocumentPermissionsOperators.CONDITION
            }, new() {
                Operator = DocumentPermissionsOperators.BLOCK
            } } }));

            Should_throw_invalid_exception_with_message(Act, "Invalid query block operators. Every even query block should be an operator (AND/OR)");
        }

        [Fact]
        public void When_validating_document_permissions_with_any_odd_query_block_not_a_condition_or_block() {
            void Act() => _subject.Execute(new(new(), new() { QueryBlocks = new() { new() {
                Operator = DocumentPermissionsOperators.AND
            }, new() {
                Operator = DocumentPermissionsOperators.OR
            }, new() {
                Operator = DocumentPermissionsOperators.OR
            } } }));

            Should_throw_invalid_exception_with_message(Act, "Invalid query block operators. Every odd query block should be a condition or block (CONDITION/BLOCK)");
        }

        private static void Should_throw_invalid_exception_with_message(Action act, string message) {
            act.Should().Throw<UksfInvalidDocumentPermissionsException>().WithMessage(message).And.StatusCode.Should().Be(400);
        }
    }
}
