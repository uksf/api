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
        public void When_validating_document_permissions_with_any_odd_query_block_not_a_condition_or_block() {
            void Act() =>
                _subject.Execute(
                    new(
                        new(),
                        new() {
                            QueryBlocks = new() {
                                new() { Operator = DocumentPermissionsOperators.AND }, new() { Operator = DocumentPermissionsOperators.OR }, new() { Operator = DocumentPermissionsOperators.OR }
                            }
                        }
                    )
                );

            Should_throw_invalid_exception_with_message(Act, "Invalid query block operators. Valid odd operators are 'CONDITION/BLOCK'");
        }

        [Fact]
        public void When_validating_document_permissions_with_any_second_query_block_not_an_operator() {
            void Act() =>
                _subject.Execute(
                    new(
                        new(),
                        new() {
                            QueryBlocks = new() {
                                new() { Operator = DocumentPermissionsOperators.CONDITION },
                                new() { Operator = DocumentPermissionsOperators.CONDITION },
                                new() { Operator = DocumentPermissionsOperators.BLOCK }
                            }
                        }
                    )
                );

            Should_throw_invalid_exception_with_message(Act, "Invalid query block operators. Valid even operators are 'AND/OR'");
        }

        [Fact]
        public void When_validating_document_permissions_with_empty_query_blocks() {
            _subject.Execute(new(new(), new() { QueryBlocks = new() }));
        }

        [Fact]
        public void When_validating_document_permissions_with_invalid_nested_operators() {
            void Act() =>
                _subject.Execute(
                    new(
                        new(),
                        new() {
                            QueryBlocks = new() {
                                new() { Operator = DocumentPermissionsOperators.CONDITION },
                                new() { Operator = DocumentPermissionsOperators.AND },
                                new() { Operator = DocumentPermissionsOperators.BLOCK, QueryBlocks = new() { new() { Operator = DocumentPermissionsOperators.AND } } }
                            }
                        }
                    )
                );

            Should_throw_invalid_exception_with_message(Act, "Invalid query block operators. Valid odd operators are 'CONDITION/BLOCK'");
        }

        [Fact]
        public void When_validating_document_permissions_with_invalid_number_of_query_blocks() {
            void Act() => _subject.Execute(new(new(), new() { QueryBlocks = new() { new(), new() } }));

            Should_throw_invalid_exception_with_message(Act, "Invalid number of query blocks. There should be an odd number of query blocks");
        }

        [Fact]
        public void When_validating_document_permissions_with_invalid_operator_for_commander_parameter() {
            void Act() =>
                _subject.Execute(
                    new(
                        new(),
                        new() {
                            QueryBlocks = new() {
                                new() {
                                    Operator = DocumentPermissionsOperators.CONDITION, Parameter = DocumentPermissionsParameters.COMMANDER, ConditionOperator = DocumentPermissionsConditionOperators.EQ
                                }
                            }
                        }
                    )
                );

            Should_throw_invalid_exception_with_message(
                Act,
                "Invalid query block condition operator for parameter 'COMMANDER'. 'EQ' is not a valid condition parameter. Valid condition parameters are 'NONE'"
            );
        }

        [Fact]
        public void When_validating_document_permissions_with_invalid_operator_for_id_parameter() {
            void Act() =>
                _subject.Execute(
                    new(
                        new(),
                        new() {
                            QueryBlocks = new() {
                                new() { Operator = DocumentPermissionsOperators.CONDITION, Parameter = DocumentPermissionsParameters.ID, ConditionOperator = DocumentPermissionsConditionOperators.EQ }
                            }
                        }
                    )
                );

            Should_throw_invalid_exception_with_message(Act, "Invalid query block condition operator for parameter 'ID'. 'EQ' is not a valid condition parameter. Valid condition parameters are 'IN'");
        }

        [Fact]
        public void When_validating_document_permissions_with_invalid_operator_for_nested_parameter() {
            void Act() =>
                _subject.Execute(
                    new(
                        new(),
                        new() {
                            QueryBlocks = new() {
                                new() { Operator = DocumentPermissionsOperators.CONDITION, Parameter = DocumentPermissionsParameters.ID, ConditionOperator = DocumentPermissionsConditionOperators.IN },
                                new() { Operator = DocumentPermissionsOperators.AND },
                                new() {
                                    Operator = DocumentPermissionsOperators.BLOCK,
                                    QueryBlocks = new() {
                                        new() {
                                            Operator = DocumentPermissionsOperators.CONDITION,
                                            Parameter = DocumentPermissionsParameters.ID,
                                            ConditionOperator = DocumentPermissionsConditionOperators.EQ
                                        }
                                    }
                                }
                            }
                        }
                    )
                );

            Should_throw_invalid_exception_with_message(Act, "Invalid query block condition operator for parameter 'ID'. 'EQ' is not a valid condition parameter. Valid condition parameters are 'IN'");
        }

        [Fact]
        public void When_validating_document_permissions_with_invalid_operator_for_rank_parameter() {
            void Act() =>
                _subject.Execute(
                    new(
                        new(),
                        new() {
                            QueryBlocks = new() {
                                new() {
                                    Operator = DocumentPermissionsOperators.CONDITION, Parameter = DocumentPermissionsParameters.RANK, ConditionOperator = DocumentPermissionsConditionOperators.IN
                                }
                            }
                        }
                    )
                );

            Should_throw_invalid_exception_with_message(
                Act,
                "Invalid query block condition operator for parameter 'RANK'. 'IN' is not a valid condition parameter. Valid condition parameters are 'EQ/NE/GT/GE/LT/LE'"
            );
        }

        [Fact]
        public void When_validating_document_permissions_with_invalid_operator_for_unit_parameter() {
            void Act() =>
                _subject.Execute(
                    new(
                        new(),
                        new() {
                            QueryBlocks = new() {
                                new() {
                                    Operator = DocumentPermissionsOperators.CONDITION, Parameter = DocumentPermissionsParameters.UNIT, ConditionOperator = DocumentPermissionsConditionOperators.EQ
                                }
                            }
                        }
                    )
                );

            Should_throw_invalid_exception_with_message(
                Act,
                "Invalid query block condition operator for parameter 'UNIT'. 'EQ' is not a valid condition parameter. Valid condition parameters are 'IN'"
            );
        }

        [Fact]
        public void When_validating_document_permissions_with_invalid_parameter() {
            void Act() => _subject.Execute(new(new(), new() { QueryBlocks = new() { new() { Operator = DocumentPermissionsOperators.CONDITION, Parameter = "random crap" } } }));

            Should_throw_invalid_exception_with_message(Act, "Invalid query block parameters. 'random crap' is not a valid parameter. Valid parameters are 'ID/UNIT/RANK/COMMANDER'");
        }

        [Fact]
        public void When_validating_document_permissions_with_null_query_blocks() {
            _subject.Execute(new(new(), new()));
        }

        private static void Should_throw_invalid_exception_with_message(Action act, string message) {
            act.Should().Throw<UksfInvalidDocumentPermissionsException>().WithMessage(message).And.StatusCode.Should().Be(400);
        }
    }
}
