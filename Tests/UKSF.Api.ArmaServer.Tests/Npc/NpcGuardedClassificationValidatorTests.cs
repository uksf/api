using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using UKSF.Api.ArmaServer.Npc.Models;
using UKSF.Api.ArmaServer.Npc.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Npc;

public class NpcGuardedClassificationValidatorTests
{
    private static List<NpcTurnDto> Turns(params (long T, string Text)[] items) =>
        items.Select(i => new NpcTurnDto
                 {
                     SpeakerId = "p",
                     Text = i.Text,
                     T = i.T
                 }
             )
             .ToList();

    [Fact]
    public void ValidOneToOne_PassesWithAddressesConcern()
    {
        var raw = new List<NpcGuardedClassification>
        {
            new()
            {
                T = 10,
                Tag = "relevant_question",
                TopicSlot = 3,
                AddressesConcern = true,
                Ambiguous = false,
                Evidence = "return"
            }
        };
        var result = NpcGuardedClassificationValidator.Validate(raw, Turns((10, "When do they return?")));
        result.Should().NotBeNull();
        result![0].Tag.Should().Be(NpcGuardedTags.RelevantQuestion);
        result[0].AddressesConcern.Should().BeTrue();
        result[0].TopicSlot.Should().Be(3);
    }

    [Fact]
    public void ExtraClassification_RejectsWhole()
    {
        var raw = new List<NpcGuardedClassification>
        {
            new()
            {
                T = 1,
                Tag = "threat",
                Evidence = "hurt",
                Ambiguous = false
            },
            new()
            {
                T = 1,
                Tag = "threat",
                Evidence = "hurt",
                Ambiguous = false
            }
        };
        NpcGuardedClassificationValidator.Validate(raw, Turns((1, "I hurt your family"))).Should().BeNull();
    }

    [Fact]
    public void MissingClassification_RejectsWhole()
    {
        var raw = new List<NpcGuardedClassification>
        {
            new()
            {
                T = 1,
                Tag = "other",
                Ambiguous = true
            }
        };
        NpcGuardedClassificationValidator.Validate(raw, Turns((1, "a"), (2, "b"))).Should().BeNull();
    }

    [Fact]
    public void ReorderedTimestamps_RejectsWhole()
    {
        var raw = new List<NpcGuardedClassification>
        {
            new()
            {
                T = 20,
                Tag = "other",
                Ambiguous = true
            },
            new()
            {
                T = 10,
                Tag = "other",
                Ambiguous = true
            }
        };
        NpcGuardedClassificationValidator.Validate(raw, Turns((10, "first"), (20, "second"))).Should().BeNull();
    }

    [Fact]
    public void TimestampMismatch_RejectsWhole()
    {
        var raw = new List<NpcGuardedClassification>
        {
            new()
            {
                T = 99,
                Tag = "other",
                Ambiguous = true
            }
        };
        NpcGuardedClassificationValidator.Validate(raw, Turns((1, "hello"))).Should().BeNull();
    }

    [Fact]
    public void UnknownTag_RejectsWhole()
    {
        var raw = new List<NpcGuardedClassification>
        {
            new()
            {
                T = 1,
                Tag = "nope",
                Evidence = "hello",
                Ambiguous = false
            }
        };
        NpcGuardedClassificationValidator.Validate(raw, Turns((1, "hello"))).Should().BeNull();
    }

    [Fact]
    public void TopicSlotOnNonQuestion_RejectsWhole()
    {
        var raw = new List<NpcGuardedClassification>
        {
            new()
            {
                T = 1,
                Tag = "threat",
                TopicSlot = 1,
                Evidence = "hurt",
                Ambiguous = false
            }
        };
        NpcGuardedClassificationValidator.Validate(raw, Turns((1, "I hurt them"))).Should().BeNull();
    }

    [Fact]
    public void MissingEvidenceOnActionable_RejectsWhole()
    {
        var raw = new List<NpcGuardedClassification>
        {
            new()
            {
                T = 1,
                Tag = "threat",
                Evidence = "",
                Ambiguous = false
            }
        };
        NpcGuardedClassificationValidator.Validate(raw, Turns((1, "I hurt your family"))).Should().BeNull();
    }

    [Fact]
    public void EvidenceNotInUtterance_RejectsWhole()
    {
        var raw = new List<NpcGuardedClassification>
        {
            new()
            {
                T = 1,
                Tag = "threat",
                Evidence = "explode",
                Ambiguous = false
            }
        };
        NpcGuardedClassificationValidator.Validate(raw, Turns((1, "I hurt your family"))).Should().BeNull();
    }

    [Fact]
    public void AmbiguousWithoutEvidence_Allowed()
    {
        var raw = new List<NpcGuardedClassification>
        {
            new()
            {
                T = 1,
                Tag = "threat",
                Evidence = "",
                Ambiguous = true
            }
        };
        var result = NpcGuardedClassificationValidator.Validate(raw, Turns((1, "mrrph")));
        result.Should().NotBeNull();
        result![0].Ambiguous.Should().BeTrue();
    }

    [Fact]
    public void AddressesConcernTag_SetsBoolean()
    {
        var raw = new List<NpcGuardedClassification>
        {
            new()
            {
                T = 1,
                Tag = "addresses_concern",
                Evidence = "safe",
                Ambiguous = false
            }
        };
        var result = NpcGuardedClassificationValidator.Validate(raw, Turns((1, "you are safe here")));
        result![0].AddressesConcern.Should().BeTrue();
        result[0].Tag.Should().Be(NpcGuardedTags.AddressesConcern);
    }
}
