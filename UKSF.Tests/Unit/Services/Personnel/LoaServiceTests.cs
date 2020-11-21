using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using UKSF.Api.Command.Context;
using UKSF.Api.Command.Services;
using UKSF.Api.Personnel.Models;
using Xunit;

namespace UKSF.Tests.Unit.Services.Personnel {
    public class LoaServiceTests {
        private readonly ILoaService _loaService;
        private readonly Mock<ILoaContext> _mockLoaDataService;

        public LoaServiceTests() {
            _mockLoaDataService = new Mock<ILoaContext>();

            _loaService = new LoaService(_mockLoaDataService.Object);
        }

        [Fact]
        public void ShouldGetCorrectLoas() {
            Loa loa1 = new() { Recipient = "5ed524b04f5b532a5437bba1", End = DateTime.Now.AddDays(-5) };
            Loa loa2 = new() { Recipient = "5ed524b04f5b532a5437bba1", End = DateTime.Now.AddDays(-35) };
            Loa loa3 = new() { Recipient = "5ed524b04f5b532a5437bba2", End = DateTime.Now.AddDays(-45) };
            Loa loa4 = new() { Recipient = "5ed524b04f5b532a5437bba2", End = DateTime.Now.AddDays(-30).AddSeconds(1) };
            Loa loa5 = new() { Recipient = "5ed524b04f5b532a5437bba3", End = DateTime.Now.AddDays(-5) };
            List<Loa> mockCollection = new() { loa1, loa2, loa3, loa4, loa5 };

            _mockLoaDataService.Setup(x => x.Get(It.IsAny<Func<Loa, bool>>())).Returns<Func<Loa, bool>>(x => mockCollection.Where(x).ToList());

            IEnumerable<Loa> subject = _loaService.Get(new List<string> { "5ed524b04f5b532a5437bba1", "5ed524b04f5b532a5437bba2" });

            subject.Should().Contain(new List<Loa> { loa1, loa4 });
        }
    }
}
