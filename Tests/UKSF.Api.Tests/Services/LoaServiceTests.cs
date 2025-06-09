using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Services;
using Xunit;

namespace UKSF.Api.Tests.Services;

public class LoaServiceTests
{
    private readonly ILoaService _loaService;
    private readonly Mock<ILoaContext> _mockLoaDataService;

    public LoaServiceTests()
    {
        _mockLoaDataService = new Mock<ILoaContext>();

        _loaService = new LoaService(_mockLoaDataService.Object);
    }

    [Fact]
    public void ShouldGetCorrectLoas()
    {
        DomainLoa loa1 = new() { Recipient = "5ed524b04f5b532a5437bba1", End = DateTime.UtcNow.AddDays(-5) };
        DomainLoa loa2 = new() { Recipient = "5ed524b04f5b532a5437bba1", End = DateTime.UtcNow.AddDays(-35) };
        DomainLoa loa3 = new() { Recipient = "5ed524b04f5b532a5437bba2", End = DateTime.UtcNow.AddDays(-45) };
        DomainLoa loa4 = new() { Recipient = "5ed524b04f5b532a5437bba2", End = DateTime.UtcNow.AddDays(-30).AddSeconds(1) };
        DomainLoa loa5 = new() { Recipient = "5ed524b04f5b532a5437bba3", End = DateTime.UtcNow.AddDays(-5) };
        List<DomainLoa> mockCollection = [loa1, loa2, loa3, loa4, loa5];

        _mockLoaDataService.Setup(x => x.Get(It.IsAny<Func<DomainLoa, bool>>())).Returns<Func<DomainLoa, bool>>(x => mockCollection.Where(x).ToList());

        var subject = _loaService.Get(["5ed524b04f5b532a5437bba1", "5ed524b04f5b532a5437bba2"]);

        subject.Should().Contain(new List<DomainLoa> { loa1, loa4 });
    }
}
