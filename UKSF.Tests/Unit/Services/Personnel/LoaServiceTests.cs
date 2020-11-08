using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services;
using UKSF.Api.Personnel.Services.Data;
using Xunit;

namespace UKSF.Tests.Unit.Services.Personnel {
    public class LoaServiceTests {
        private readonly ILoaService loaService;
        private readonly Mock<ILoaDataService> mockLoaDataService;

        public LoaServiceTests() {
            mockLoaDataService = new Mock<ILoaDataService>();

            loaService = new LoaService(mockLoaDataService.Object);
        }

        [Fact]
        public void ShouldGetCorrectLoas() {
            Loa loa1 = new Loa { recipient = "5ed524b04f5b532a5437bba1", end = DateTime.Now.AddDays(-5) };
            Loa loa2 = new Loa { recipient = "5ed524b04f5b532a5437bba1", end = DateTime.Now.AddDays(-35) };
            Loa loa3 = new Loa { recipient = "5ed524b04f5b532a5437bba2", end = DateTime.Now.AddDays(-45) };
            Loa loa4 = new Loa { recipient = "5ed524b04f5b532a5437bba2", end = DateTime.Now.AddDays(-30).AddSeconds(1) };
            Loa loa5 = new Loa { recipient = "5ed524b04f5b532a5437bba3", end = DateTime.Now.AddDays(-5) };
            List<Loa> mockCollection = new List<Loa> { loa1, loa2, loa3, loa4, loa5 };

            mockLoaDataService.Setup(x => x.Get(It.IsAny<Func<Loa, bool>>())).Returns<Func<Loa, bool>>(x => mockCollection.Where(x).ToList());

            IEnumerable<Loa> subject = loaService.Get(new List<string> { "5ed524b04f5b532a5437bba1", "5ed524b04f5b532a5437bba2" });

            subject.Should().Contain(new List<Loa> { loa1, loa4 });
        }
    }
}
