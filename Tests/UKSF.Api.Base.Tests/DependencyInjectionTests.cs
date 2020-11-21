using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Base.Context;
using UKSF.Api.Tests.Common;
using Xunit;

namespace UKSF.Api.Base.Tests {
    public class DependencyInjectionTests : DependencyInjectionTestsBase {
        public DependencyInjectionTests() {
            Services.AddUksfBase(TestConfiguration);
        }

        [Fact]
        public void When_resolving_MongoCollectionFactory() {
            Services.AddTransient<IMongoCollectionFactory, MongoCollectionFactory>();
            ServiceProvider serviceProvider = Services.BuildServiceProvider();

            IMongoCollectionFactory subject = serviceProvider.GetRequiredService<IMongoCollectionFactory>();

            subject.Should().NotBeNull();
        }
    }
}
