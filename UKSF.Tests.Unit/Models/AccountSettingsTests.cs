﻿using FluentAssertions;
using UKSF.Api.Models.Personnel;
using Xunit;

namespace UKSF.Tests.Unit.Models {
    public class AccountSettingsTests {
        [Fact]
        public void ShouldReturnBool() {
            AccountSettings subject = new AccountSettings();

            bool attribute = subject.GetAttribute<bool>("sr1Enabled");

            attribute.GetType().Should().Be(typeof(bool));
        }
        
        [Fact]
        public void ShouldReturnCorrectValue() {
            AccountSettings subject = new AccountSettings {sr1Enabled = false, errorEmails = true};

            bool sr1Enabled = subject.GetAttribute<bool>("sr1Enabled");
            bool errorEmails = subject.GetAttribute<bool>("errorEmails");

            sr1Enabled.Should().BeFalse();
            errorEmails.Should().BeTrue();
        }
    }
}