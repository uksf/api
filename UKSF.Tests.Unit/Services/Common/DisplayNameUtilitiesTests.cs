using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Personnel;
using UKSF.Api.Interfaces.Units;
using UKSF.Api.Services.Common;
using Xunit;

namespace UKSF.Tests.Unit.Services.Common {
    public class DisplayNameUtilitiesTests {
        private readonly Mock<IDisplayNameService> mockDisplayNameService;
        private readonly Mock<IUnitsDataService> mockUnitsDataService;

        public DisplayNameUtilitiesTests() {
            mockDisplayNameService = new Mock<IDisplayNameService>();
            mockUnitsDataService = new Mock<IUnitsDataService>();
            Mock<IUnitsService> mockUnitsService = new Mock<IUnitsService>();

            mockUnitsService.Setup(x => x.Data).Returns(mockUnitsDataService.Object);

            ServiceCollection serviceProvider = new ServiceCollection();
            serviceProvider.AddTransient(provider => mockDisplayNameService.Object);
            serviceProvider.AddTransient(provider => mockUnitsService.Object);
            ServiceWrapper.ServiceProvider = serviceProvider.BuildServiceProvider();
        }

        [Theory, InlineData("5e39336e1b92ee2d14b7fe08", "Maj.Bridgford.A"), InlineData("5e39336e1b92ee2d14b7fe08, 5e3935db1b92ee2d14b7fe09", "Maj.Bridgford.A, Cpl.Carr.C"), InlineData("5e39336e1b92ee2d14b7fe085e3935db1b92ee2d14b7fe09", "Maj.Bridgford.ACpl.Carr.C"),
         InlineData("5e39336e1b92ee2d14b7fe08 has requested all the things for 5e3935db1b92ee2d14b7fe09", "Maj.Bridgford.A has requested all the things for Cpl.Carr.C")]
        public void ShouldConvertNameObjectIds(string input, string expected) {
            mockUnitsDataService.Setup(x => x.GetSingle(It.IsAny<Func<Api.Models.Units.Unit, bool>>())).Returns<Api.Models.Units.Unit>(null);
            mockDisplayNameService.Setup(x => x.GetDisplayName("5e39336e1b92ee2d14b7fe08")).Returns("Maj.Bridgford.A");
            mockDisplayNameService.Setup(x => x.GetDisplayName("5e3935db1b92ee2d14b7fe09")).Returns("Cpl.Carr.C");

            string subject = input.ConvertObjectIds();

            subject.Should().Be(expected);
        }

        [Fact]
        public void ShouldConvertCorrectUnitWithPredicate() {
            Api.Models.Units.Unit unit1 = new Api.Models.Units.Unit {name = "7 Squadron"};
            Api.Models.Units.Unit unit2 = new Api.Models.Units.Unit {name = "656 Squadron"};
            List<Api.Models.Units.Unit> collection = new List<Api.Models.Units.Unit> {unit1, unit2};

            mockUnitsDataService.Setup(x => x.GetSingle(It.IsAny<Func<Api.Models.Units.Unit, bool>>())).Returns<Func<Api.Models.Units.Unit, bool>>(x => collection.FirstOrDefault(x));
            mockDisplayNameService.Setup(x => x.GetDisplayName(It.IsAny<string>())).Returns<string>(x => x);

            string subject = unit1.id.ConvertObjectIds();

            subject.Should().Be("7 Squadron");
        }

        [Fact]
        public void ShouldConvertUnitObjectIds() {
            const string INPUT = "5e39336e1b92ee2d14b7fe08";
            const string EXPECTED = "7 Squadron";
            Api.Models.Units.Unit unit = new Api.Models.Units.Unit {name = EXPECTED, id = INPUT};

            mockUnitsDataService.Setup(x => x.GetSingle(It.IsAny<Func<Api.Models.Units.Unit, bool>>())).Returns(unit);
            mockDisplayNameService.Setup(x => x.GetDisplayName(It.IsAny<string>())).Returns<string>(x => x);

            string subject = INPUT.ConvertObjectIds();

            subject.Should().Be(EXPECTED);
        }

        [Fact]
        public void ShouldDoNothingToTextWhenNameOrUnitNotFound() {
            const string INPUT = "5e39336e1b92ee2d14b7fe08";
            const string EXPECTED = "5e39336e1b92ee2d14b7fe08";

            mockUnitsDataService.Setup(x => x.GetSingle(It.IsAny<Func<Api.Models.Units.Unit, bool>>())).Returns<Api.Models.Units.Unit>(null);
            mockDisplayNameService.Setup(x => x.GetDisplayName(It.IsAny<string>())).Returns<string>(x => x);

            string subject = INPUT.ConvertObjectIds();

            subject.Should().Be(EXPECTED);
        }

        [Fact]
        public void ShouldReturnEmpty() {
            string subject = "".ConvertObjectIds();

            subject.Should().Be(string.Empty);
        }
    }
}
