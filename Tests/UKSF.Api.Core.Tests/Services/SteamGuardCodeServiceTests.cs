using System;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using UKSF.Api.Core.Configuration;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.Core.Tests.Services;

public class SteamGuardCodeServiceTests
{
    // base64 of the ASCII bytes "12345678901234567890". Codes below were verified against an independent Steam TOTP implementation.
    private const string KnownSecret = "MTIzNDU2Nzg5MDEyMzQ1Njc4OTA=";

    private readonly Mock<IClock> _mockClock = new();

    private SteamGuardCodeService CreateSubject(string sharedSecret)
    {
        var appSettings = new AppSettings
        {
            Secrets = new AppSettings.SecretsConfig { SteamCmd = new AppSettings.SecretsConfig.SteamCmdConfig { SharedSecret = sharedSecret } }
        };
        return new SteamGuardCodeService(Options.Create(appSettings), _mockClock.Object);
    }

    private void GivenTime(long unixSeconds)
    {
        _mockClock.Setup(x => x.UtcNow()).Returns(DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime);
    }

    [Theory]
    [InlineData(1600000000, "J8D72")]
    [InlineData(1600000029, "KWCH7")]
    [InlineData(1700000000, "R87JJ")]
    public void GenerateCode_WithKnownSecretAndTime_ReturnsExpectedCode(long unixSeconds, string expected)
    {
        GivenTime(unixSeconds);

        CreateSubject(KnownSecret).GenerateCode().Should().Be(expected);
    }

    [Theory]
    [InlineData(1600000000)]
    [InlineData(1600000010)]
    [InlineData(1600000019)]
    public void GenerateCode_WithinSameThirtySecondStep_ReturnsSameCode(long unixSeconds)
    {
        GivenTime(unixSeconds);

        CreateSubject(KnownSecret).GenerateCode().Should().Be("J8D72");
    }

    [Fact]
    public void GenerateCode_AtNextStepBoundary_ChangesCode()
    {
        GivenTime(1600000020);

        CreateSubject(KnownSecret).GenerateCode().Should().Be("KWCH7");
    }

    [Fact]
    public void GenerateCode_AlwaysReturnsFiveCharactersFromSteamAlphabet()
    {
        GivenTime(1700000000);

        var code = CreateSubject(KnownSecret).GenerateCode();

        code.Should().HaveLength(5);
        code.Should().MatchRegex("^[23456789BCDFGHJKMNPQRTVWXY]{5}$");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GenerateCode_WhenNoSharedSecretConfigured_ReturnsNull(string sharedSecret)
    {
        GivenTime(1600000000);

        CreateSubject(sharedSecret).GenerateCode().Should().BeNull();
    }

    [Theory]
    [InlineData(KnownSecret, true)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    public void IsConfigured_ReflectsWhetherSharedSecretIsSet(string sharedSecret, bool expected)
    {
        CreateSubject(sharedSecret).IsConfigured.Should().Be(expected);
    }

    [Theory]
    [InlineData(1600000000, 20)] // 10s into the 30s window -> 20s remaining
    [InlineData(1600000029, 21)] // 9s into the window -> 21s remaining
    [InlineData(1600000020, 30)] // exactly on a boundary -> full window remaining
    public void TimeUntilNextCode_ReturnsSecondsRemainingInWindow(long unixSeconds, double expectedSeconds)
    {
        GivenTime(unixSeconds);

        CreateSubject(KnownSecret).TimeUntilNextCode().TotalSeconds.Should().BeApproximately(expectedSeconds, 0.001);
    }
}
