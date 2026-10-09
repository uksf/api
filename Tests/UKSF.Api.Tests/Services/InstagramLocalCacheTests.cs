using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using UKSF.Api.Integrations.Instagram.Services;
using Xunit;

namespace UKSF.Api.Tests.Services;

public sealed class InstagramLocalCacheTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"instagram-cache-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private static InstagramService Subject(string folder)
    {
        Mock<IVariablesService> variables = new();
        variables.Setup(x => x.GetVariable("INSTAGRAM_LOCAL_CACHE")).Returns(new DomainVariableItem { Key = "INSTAGRAM_LOCAL_CACHE", Item = folder });
        return new InstagramService(new Mock<IVariablesContext>().Object, variables.Object, new Mock<IHttpClientFactory>().Object, new Mock<IUksfLogger>().Object);
    }

    [Fact]
    public async Task GetImagesFromLocalCache_WhenTheFolderDoesNotExist_ReturnsNoImages()
    {
        var images = await Subject(_folder).GetImagesFromLocalCache();

        images.Should().BeEmpty();
    }

    [Fact]
    public async Task GetImagesFromLocalCache_ReturnsEachCachedImageAsBase64()
    {
        Directory.CreateDirectory(_folder);
        await File.WriteAllBytesAsync(Path.Combine(_folder, "op-1.jpg"), [1, 2, 3]);

        var images = await Subject(_folder).GetImagesFromLocalCache();

        images.Should().ContainSingle();
        images[0].Id.Should().Be("op-1");
        images[0].Base64.Should().Be("data:image/jpeg;base64,AQID");
    }
}
