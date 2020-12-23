using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Admin;
using UKSF.Api.Documents.Controllers;
using UKSF.Api.Personnel;
using UKSF.Api.Tests.Common;
using Xunit;

namespace UKSF.Api.Documents.Tests {
    public class DependencyInjectionTests : DependencyInjectionTestsBase {
        public DependencyInjectionTests() {
            Services.AddUksfAdmin();
            Services.AddUksfPersonnel();
            Services.AddUksfDocuments();
        }

        [Fact]
        public void When_resolving_controllers() {
            Services.AddTransient<DocumentsController>();
            Services.AddTransient<ArchivedDocumentsController>();
            ServiceProvider serviceProvider = Services.BuildServiceProvider();

            serviceProvider.GetRequiredService<DocumentsController>().Should().NotBeNull();
            serviceProvider.GetRequiredService<ArchivedDocumentsController>().Should().NotBeNull();
        }

        [Fact]
        public void When_resolving_event_handlers() {
            // Services.AddTransient<CommandRequestEventHandler>();
            // ServiceProvider serviceProvider = Services.BuildServiceProvider();
            //
            // serviceProvider.GetRequiredService<CommandRequestEventHandler>().Should().NotBeNull();
        }
    }
}
