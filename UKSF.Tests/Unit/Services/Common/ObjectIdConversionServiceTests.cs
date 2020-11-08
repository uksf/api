using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Services;
using Xunit;

namespace UKSF.Tests.Unit.Services.Common {
    public class ObjectIdConversionServiceTests {
        private readonly Mock<IDisplayNameService> mockDisplayNameService;
        private readonly Mock<IUnitsDataService> mockUnitsDataService;
        private readonly Mock<IUnitsService> mockUnitsService;
        private readonly ObjectIdConversionService objectIdConversionService;

        public ObjectIdConversionServiceTests() {
            mockDisplayNameService = new Mock<IDisplayNameService>();
            mockUnitsService = new Mock<IUnitsService>();
            mockUnitsDataService = new Mock<IUnitsDataService>();

            mockUnitsService.Setup(x => x.Data).Returns(mockUnitsDataService.Object);
            objectIdConversionService = new ObjectIdConversionService(mockDisplayNameService.Object, mockUnitsService.Object);
        }

        [Theory, InlineData("5e39336e1b92ee2d14b7fe08", "Maj.Bridgford.A"), InlineData("5e39336e1b92ee2d14b7fe08, 5e3935db1b92ee2d14b7fe09", "Maj.Bridgford.A, Cpl.Carr.C"),
         InlineData("5e39336e1b92ee2d14b7fe085e3935db1b92ee2d14b7fe09", "Maj.Bridgford.ACpl.Carr.C"),
         InlineData("5e39336e1b92ee2d14b7fe08 has requested all the things for 5e3935db1b92ee2d14b7fe09", "Maj.Bridgford.A has requested all the things for Cpl.Carr.C")]
        public void ShouldConvertNameObjectIds(string input, string expected) {
            mockUnitsDataService.Setup(x => x.GetSingle(It.IsAny<Func<Api.Personnel.Models.Unit, bool>>())).Returns<Api.Personnel.Models.Unit>(null);
            mockDisplayNameService.Setup(x => x.GetDisplayName("5e39336e1b92ee2d14b7fe08")).Returns("Maj.Bridgford.A");
            mockDisplayNameService.Setup(x => x.GetDisplayName("5e3935db1b92ee2d14b7fe09")).Returns("Cpl.Carr.C");

            string subject = objectIdConversionService.ConvertObjectIds(input);

            subject.Should().Be(expected);
        }

        [Fact]
        public void ShouldConvertCorrectUnitWithPredicate() {
            Api.Personnel.Models.Unit unit1 = new Api.Personnel.Models.Unit { name = "7 Squadron" };
            Api.Personnel.Models.Unit unit2 = new Api.Personnel.Models.Unit { name = "656 Squadron" };
            List<Api.Personnel.Models.Unit> collection = new List<Api.Personnel.Models.Unit> { unit1, unit2 };

            mockUnitsDataService.Setup(x => x.GetSingle(It.IsAny<Func<Api.Personnel.Models.Unit, bool>>())).Returns<Func<Api.Personnel.Models.Unit, bool>>(x => collection.FirstOrDefault(x));
            mockDisplayNameService.Setup(x => x.GetDisplayName(It.IsAny<string>())).Returns<string>(x => x);

            string subject = objectIdConversionService.ConvertObjectIds(unit1.id);

            subject.Should().Be("7 Squadron");
        }

        [Fact]
        public void ShouldConvertUnitObjectIds() {
            const string INPUT = "5e39336e1b92ee2d14b7fe08";
            const string EXPECTED = "7 Squadron";
            Api.Personnel.Models.Unit unit = new Api.Personnel.Models.Unit { name = EXPECTED, id = INPUT };

            mockUnitsDataService.Setup(x => x.GetSingle(It.IsAny<Func<Api.Personnel.Models.Unit, bool>>())).Returns(unit);
            mockDisplayNameService.Setup(x => x.GetDisplayName(It.IsAny<string>())).Returns<string>(x => x);

            string subject = objectIdConversionService.ConvertObjectIds(INPUT);

            subject.Should().Be(EXPECTED);
        }

        [Fact]
        public void ShouldDoNothingToTextWhenNameOrUnitNotFound() {
            const string INPUT = "5e39336e1b92ee2d14b7fe08";
            const string EXPECTED = "5e39336e1b92ee2d14b7fe08";

            mockUnitsDataService.Setup(x => x.GetSingle(It.IsAny<Func<Api.Personnel.Models.Unit, bool>>())).Returns<Api.Personnel.Models.Unit>(null);
            mockDisplayNameService.Setup(x => x.GetDisplayName(It.IsAny<string>())).Returns<string>(x => x);

            string subject = objectIdConversionService.ConvertObjectIds(INPUT);

            subject.Should().Be(EXPECTED);
        }

        [Fact]
        public void ShouldReturnEmpty() {
            string subject = objectIdConversionService.ConvertObjectIds("");

            subject.Should().Be(string.Empty);
        }
    }
}
