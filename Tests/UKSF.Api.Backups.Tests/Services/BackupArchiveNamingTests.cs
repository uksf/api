using System;
using FluentAssertions;
using UKSF.Api.Backups.Services;
using Xunit;

namespace UKSF.Api.Backups.Tests.Services;

public class BackupArchiveNamingTests
{
    [Fact]
    public void A_name_is_built_from_the_run_time()
    {
        BackupArchiveNaming.ForTime(new DateTime(2026, 8, 2, 4, 0, 0, DateTimeKind.Utc)).Should().Be("uksf-backup-20260802-040000.zip.enc");
    }

    [Theory]
    [InlineData("uksf-backup-20260802-040000.zip.enc", true)]
    [InlineData(@"E:\Backups\uksf-backup-20260802-040000.zip.enc", true)]
    [InlineData("UKSF-BACKUP-20260802-040000.ZIP.ENC", true)]
    [InlineData("uksf-backup-20260802-040000.zip", false)]
    [InlineData("uksf-backup.zip.enc", false)]
    [InlineData("holiday-photo.jpg", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Only_real_archive_names_are_recognised(string name, bool expected)
    {
        BackupArchiveNaming.IsArchive(name).Should().Be(expected);
    }

    [Fact]
    public void Sorting_uses_the_stamp_in_the_name_not_the_file_time()
    {
        var older = BackupArchiveNaming.SortKey(@"E:\Backups\uksf-backup-20260801-040000.zip.enc");
        var newer = BackupArchiveNaming.SortKey("uksf-backup-20260802-035959.zip.enc");

        newer.Should().BeAfter(older);
    }

    [Fact]
    public void A_name_that_is_not_an_archive_sorts_last()
    {
        BackupArchiveNaming.SortKey("notes.txt").Should().Be(DateTime.MinValue);
    }
}
