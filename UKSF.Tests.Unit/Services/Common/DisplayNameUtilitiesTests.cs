using System;
using System.Data;
using FluentAssertions;
using Moq;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Personnel;
using UKSF.Api.Interfaces.Units;
using UKSF.Api.Services.Common;
using Xunit;

namespace UKSF.Tests.Unit.Services.Common {
    public class DisplayNameUtilitiesTests {
        // [Fact]
        // public void ShouldConvertNameObjectIds() {
        //     // Api.Models.Units.Unit unit = new Api.Models.Units.Unit {name = "7 Squadron"};
        //     const string EXPECTED = "Maj.Bridgford.A has requested all the things for Cpl.Carr.C";
        //
        //     Mock<IDisplayNameService> mockDisplayNameService = new Mock<IDisplayNameService>();
        //     Mock<IUnitsDataService> mockUnitsDataService = new Mock<IUnitsDataService>();
        //     Mock<IUnitsService> mockUnitsService = new Mock<IUnitsService>();
        //
        //     mockUnitsDataService.Setup(x => x.GetSingle(It.IsAny<Func<Api.Models.Units.Unit, bool>>())).Returns<Api.Models.Units.Unit>(null);
        //     mockUnitsService.Setup(x => x.Data()).Returns(mockUnitsDataService.Object);
        //     mockDisplayNameService.Setup(x => x.GetDisplayName("5e39336e1b92ee2d14b7fe08")).Returns("Maj.Bridgford.A");
        //     mockDisplayNameService.Setup(x => x.GetDisplayName("5e3935db1b92ee2d14b7fe09")).Returns("Cpl.Carr.C");
        //
        //     string subject = "5e39336e1b92ee2d14b7fe08 has requested all the things for 5e3935db1b92ee2d14b7fe09".ConvertObjectIds();
        //
        //     subject.Should().Be(EXPECTED);
        // }
    }
}

