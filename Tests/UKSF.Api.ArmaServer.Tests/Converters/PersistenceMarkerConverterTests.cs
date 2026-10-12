using System;
using System.Collections.Generic;
using FluentAssertions;
using UKSF.Api.ArmaServer.Converters;
using UKSF.Api.ArmaServer.Models.Persistence;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Converters;

public class PersistenceMarkerConverterTests
{
    private static List<object> Icon() =>
    [
        "_USER_DEFINED #1/5/1",
        new List<object> { 18642L, 1641.53, 0L },
        "ICON",
        "GRG_Marker",
        "Solid",
        new List<object> { 1L, 1L },
        "ColorGreen",
        1L,
        42.4505,
        "X-2"
    ];

    private static List<object> Polyline() =>
    [
        "_USER_DEFINED #1/0/1",
        new List<object> { 17277.3, 2138.69, 0L },
        "POLYLINE",
        "ColorGreen",
        1L,
        new List<object> { 17277.3, 2138.69, 17178.1, 1482.28 }
    ];

    [Fact]
    public void FromList_Icon_NamesEveryField()
    {
        var marker = PersistenceMarkerConverter.FromList(Icon());

        marker.Should()
        .BeEquivalentTo(
            new PersistenceMarker
            {
                Name = "_USER_DEFINED #1/5/1",
                Position = [18642, 1641.53, 0],
                Shape = "ICON",
                Type = "GRG_Marker",
                Brush = "Solid",
                Size = [1, 1],
                Colour = "ColorGreen",
                Alpha = 1,
                Direction = 42.4505,
                Text = "X-2"
            }
        );
    }

    [Fact]
    public void FromList_Polyline_KeepsThePointsAndLeavesIconFieldsEmpty()
    {
        var marker = PersistenceMarkerConverter.FromList(Polyline());

        marker.Should()
        .BeEquivalentTo(
            new PersistenceMarker
            {
                Name = "_USER_DEFINED #1/0/1",
                Position = [17277.3, 2138.69, 0],
                Shape = "POLYLINE",
                Colour = "ColorGreen",
                Alpha = 1,
                Polyline = [17277.3, 2138.69, 17178.1, 1482.28]
            }
        );
    }

    [Fact]
    public void ToList_RebuildsTheSqfFieldOrderForBothShapes()
    {
        PersistenceMarkerConverter.ToList(PersistenceMarkerConverter.FromList(Icon()))
                                  .Should()
                                  .BeEquivalentTo(
                                      new List<object>
                                      {
                                          "_USER_DEFINED #1/5/1",
                                          new List<object> { 18642.0, 1641.53, 0.0 },
                                          "ICON",
                                          "GRG_Marker",
                                          "Solid",
                                          new List<object> { 1.0, 1.0 },
                                          "ColorGreen",
                                          1.0,
                                          42.4505,
                                          "X-2"
                                      },
                                      o => o.WithStrictOrdering()
                                  );

        PersistenceMarkerConverter.ToList(PersistenceMarkerConverter.FromList(Polyline()))
                                  .Should()
                                  .BeEquivalentTo(
                                      new List<object>
                                      {
                                          "_USER_DEFINED #1/0/1",
                                          new List<object> { 17277.3, 2138.69, 0.0 },
                                          "POLYLINE",
                                          "ColorGreen",
                                          1.0,
                                          new List<object> { 17277.3, 2138.69, 17178.1, 1482.28 }
                                      },
                                      o => o.WithStrictOrdering()
                                  );
    }

    [Theory]
    [InlineData(6, "ICON")]
    [InlineData(10, "POLYLINE")]
    [InlineData(3, "ICON")]
    public void FromList_FieldCountThatDoesNotMatchTheShape_Throws(int count, string shape)
    {
        var raw = new List<object> { "m", new List<object> { 1.0, 2.0, 0.0 }, shape };
        while (raw.Count < count)
        {
            raw.Add("x");
        }

        var act = () => PersistenceMarkerConverter.FromList(raw);

        act.Should().Throw<FormatException>().WithMessage($"*'m'*{count}*{shape}*");
    }

    [Fact]
    public void SessionFromHashmap_SkipsEmptyMarkerEntries()
    {
        var raw = new Dictionary<string, object> { ["mapMarkers"] = new List<object> { new List<object>(), Icon() } };

        PersistenceConverter.FromHashmap(raw).Markers.Should().ContainSingle().Which.Name.Should().Be("_USER_DEFINED #1/5/1");
    }
}
