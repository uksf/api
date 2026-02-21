using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Queries;
using Xunit;

namespace UKSF.Api.Tests.Queries;

public class AllNationsByAccountQueryTests
{
    private readonly Mock<IAccountContext> _mockAccountContext = new();
    private readonly AllNationsByAccountQuery _subject;

    public AllNationsByAccountQueryTests()
    {
        _subject = new AllNationsByAccountQuery(_mockAccountContext.Object);
    }

    [Fact]
    public async Task ExecuteAsync_returns_nations_ordered_by_count_descending()
    {
        var accounts = new List<DomainAccount>
        {
            new() { Nation = "UK" },
            new() { Nation = "UK" },
            new() { Nation = "UK" },
            new() { Nation = "US" },
            new() { Nation = "US" },
            new() { Nation = "Germany" }
        };
        _mockAccountContext.Setup(x => x.Get()).Returns(accounts);

        var result = await _subject.ExecuteAsync();

        result.Should().Equal("UK", "US", "Germany");
    }

    [Fact]
    public async Task ExecuteAsync_orders_by_name_when_count_equal()
    {
        var accounts = new List<DomainAccount>
        {
            new() { Nation = "Germany" },
            new() { Nation = "Germany" },
            new() { Nation = "US" },
            new() { Nation = "US" }
        };
        _mockAccountContext.Setup(x => x.Get()).Returns(accounts);

        var result = await _subject.ExecuteAsync();

        result.Should().Equal("Germany", "US");
    }

    [Fact]
    public async Task ExecuteAsync_excludes_null_and_whitespace_nations()
    {
        var accounts = new List<DomainAccount>
        {
            new() { Nation = "UK" },
            new() { Nation = null },
            new() { Nation = "" },
            new() { Nation = " " }
        };
        _mockAccountContext.Setup(x => x.Get()).Returns(accounts);

        var result = await _subject.ExecuteAsync();

        result.Should().Equal("UK");
    }

    [Fact]
    public async Task ExecuteAsync_returns_empty_when_no_accounts()
    {
        _mockAccountContext.Setup(x => x.Get()).Returns(new List<DomainAccount>());

        var result = await _subject.ExecuteAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_deduplicates_nations()
    {
        var accounts = new List<DomainAccount>
        {
            new() { Nation = "UK" },
            new() { Nation = "UK" },
            new() { Nation = "UK" }
        };
        _mockAccountContext.Setup(x => x.Get()).Returns(accounts);

        var result = await _subject.ExecuteAsync();

        result.Should().ContainSingle().Which.Should().Be("UK");
    }
}
