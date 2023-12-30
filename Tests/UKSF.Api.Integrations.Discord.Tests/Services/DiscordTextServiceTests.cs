using FluentAssertions;
using UKSF.Api.Integrations.Discord.Services;
using Xunit;

namespace UKSF.Api.Integrations.Discord.Tests.Services;

public class DiscordTextServiceTests
{
    private readonly DiscordTextService _subject = new();

    [Fact]
    public void When_changing_to_quote()
    {
        const string Text = "**Changed**" +
                            "\n- Apache - No fuel state on HUD [(#998)](<https://github.com/uksf/modpack/issues/998>)" +
                            "\n- Virtualisation - Allow groups to have vehicles [(#1015)](<https://github.com/uksf/modpack/issues/1015>)" +
                            "\n" +
                            "\n**Fixed**" +
                            "\n- MQ-9 Reaper - AI dropping munitions when drone is not in use [(#1022)](<https://github.com/uksf/modpack/issues/1022>)" +
                            "\n- Virtualisation - Patrolling vehicles remain static [(#1017)](<https://github.com/uksf/modpack/issues/1017>)" +
                            "\n" +
                            "\nSR3 - Development Team" +
                            "\n[Report and track issues here](<https://github.com/uksf/modpack/issues>)";

        var result = _subject.ToQuote(Text);

        result.Should()
              .Be(
                  "> **Changed**" +
                  "\n> - Apache - No fuel state on HUD [(#998)](<https://github.com/uksf/modpack/issues/998>)" +
                  "\n> - Virtualisation - Allow groups to have vehicles [(#1015)](<https://github.com/uksf/modpack/issues/1015>)" +
                  "\n> " +
                  "\n> **Fixed**" +
                  "\n> - MQ-9 Reaper - AI dropping munitions when drone is not in use [(#1022)](<https://github.com/uksf/modpack/issues/1022>)" +
                  "\n> - Virtualisation - Patrolling vehicles remain static [(#1017)](<https://github.com/uksf/modpack/issues/1017>)" +
                  "\n> " +
                  "\n> SR3 - Development Team" +
                  "\n> [Report and track issues here](<https://github.com/uksf/modpack/issues>)"
              );
    }

    [Fact]
    public void When_converting_from_markdown()
    {
        const string Markdown = "#### Changed" +
                                "\n- Apache - No fuel state on HUD [(#998)](https://github.com/uksf/modpack/issues/998)" +
                                "\n- Virtualisation - Allow groups to have vehicles [(#1015)](https://github.com/uksf/modpack/issues/1015)" +
                                "\n" +
                                "\n#### Fixed" +
                                "\n- MQ-9 Reaper - AI dropping munitions when drone is not in use [(#1022)](https://github.com/uksf/modpack/issues/1022)" +
                                "\n- Virtualisation - Patrolling vehicles remain static [(#1017)](https://github.com/uksf/modpack/issues/1017)" +
                                "\n" +
                                "\n<br>SR3 - Development Team<br>[Report and track issues here](https://github.com/uksf/modpack/issues)";

        var result = _subject.FromMarkdown(Markdown);

        result.Should()
              .Be(
                  "**Changed**" +
                  "\n- Apache - No fuel state on HUD [(#998)](<https://github.com/uksf/modpack/issues/998>)" +
                  "\n- Virtualisation - Allow groups to have vehicles [(#1015)](<https://github.com/uksf/modpack/issues/1015>)" +
                  "\n" +
                  "\n**Fixed**" +
                  "\n- MQ-9 Reaper - AI dropping munitions when drone is not in use [(#1022)](<https://github.com/uksf/modpack/issues/1022>)" +
                  "\n- Virtualisation - Patrolling vehicles remain static [(#1017)](<https://github.com/uksf/modpack/issues/1017>)" +
                  "\n" +
                  "\nSR3 - Development Team" +
                  "\n[Report and track issues here](<https://github.com/uksf/modpack/issues>)"
              );
    }
}
