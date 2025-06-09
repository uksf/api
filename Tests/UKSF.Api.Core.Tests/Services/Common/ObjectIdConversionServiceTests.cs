using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.Core.Tests.Services.Common;

public class ObjectIdConversionServiceTests
{
    private readonly Mock<IDisplayNameService> _mockDisplayNameService;
    private readonly Mock<IUnitsContext> _mockUnitsContext;
    private readonly ObjectIdConversionService _objectIdConversionService;

    public ObjectIdConversionServiceTests()
    {
        _mockDisplayNameService = new Mock<IDisplayNameService>();
        _mockUnitsContext = new Mock<IUnitsContext>();

        _objectIdConversionService = new ObjectIdConversionService(_mockUnitsContext.Object, _mockDisplayNameService.Object);
    }

    [Fact]
    public void ShouldConvertCorrectUnitWithPredicate()
    {
        DomainUnit unit1 = new() { Name = "7 Squadron" };
        DomainUnit unit2 = new() { Name = "656 Squadron" };
        List<DomainUnit> collection = [unit1, unit2];

        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>())).Returns<Func<DomainUnit, bool>>(x => collection.FirstOrDefault(x));
        _mockDisplayNameService.Setup(x => x.GetDisplayName(It.IsAny<string>())).Returns<string>(x => x);

        var subject = _objectIdConversionService.ConvertObjectIds(unit1.Id);

        subject.Should().Be("7 Squadron");
    }

    [Fact]
    public void ShouldConvertUnitObjectIds()
    {
        const string Input = "5e39336e1b92ee2d14b7fe08";
        const string Expected = "7 Squadron";
        DomainUnit unit = new() { Name = Expected, Id = Input };

        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>())).Returns(unit);
        _mockDisplayNameService.Setup(x => x.GetDisplayName(It.IsAny<string>())).Returns<string>(x => x);

        var subject = _objectIdConversionService.ConvertObjectIds(Input);

        subject.Should().Be(Expected);
    }

    [Fact]
    public void ShouldDoNothingToTextWhenNameOrUnitNotFound()
    {
        const string Input = "5e39336e1b92ee2d14b7fe08";
        const string Expected = "5e39336e1b92ee2d14b7fe08";

        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>())).Returns<DomainUnit>(null);
        _mockDisplayNameService.Setup(x => x.GetDisplayName(It.IsAny<string>())).Returns<string>(x => x);

        var subject = _objectIdConversionService.ConvertObjectIds(Input);

        subject.Should().Be(Expected);
    }

    [Fact]
    public void ShouldReturnEmpty()
    {
        var subject = _objectIdConversionService.ConvertObjectIds("");

        subject.Should().Be(string.Empty);
    }

    [Theory]
    [InlineData("5e39336e1b92ee2d14b7fe08", "Maj.Bridgford.A")]
    [InlineData("5e39336e1b92ee2d14b7fe08, 5e3935db1b92ee2d14b7fe09", "Maj.Bridgford.A, Cpl.Carr.C")]
    [InlineData("5e39336e1b92ee2d14b7fe085e3935db1b92ee2d14b7fe09", "Maj.Bridgford.ACpl.Carr.C")]
    [InlineData(
        "5e39336e1b92ee2d14b7fe08 has requested all the things for 5e3935db1b92ee2d14b7fe09",
        "Maj.Bridgford.A has requested all the things for Cpl.Carr.C"
    )]
    public void ShouldConvertNameObjectIds(string input, string expected)
    {
        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>())).Returns<DomainUnit>(null);
        _mockDisplayNameService.Setup(x => x.GetDisplayName("5e39336e1b92ee2d14b7fe08")).Returns("Maj.Bridgford.A");
        _mockDisplayNameService.Setup(x => x.GetDisplayName("5e3935db1b92ee2d14b7fe09")).Returns("Cpl.Carr.C");

        var subject = _objectIdConversionService.ConvertObjectIds(input);

        subject.Should().Be(expected);
    }
}
