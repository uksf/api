using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSFWebsite.Api.Models.Documents;
using UKSFWebsite.Api.Services.Abstraction;

namespace UKSFWebsite.Api.Controllers.Documents {
    [Route("[controller]")]
    public class DocumentsController : Controller {
        private readonly IDocumentService documentService;
        private readonly ISessionService sessionService;

        public DocumentsController(IDocumentService documentService, ISessionService sessionService) {
            this.documentService = documentService;
            this.sessionService = sessionService;
        }

        [HttpGet("about")]
        public async Task<IActionResult> GetAbout() {
            Document document = documentService.GetSingle(x => x.directory == "about");
            DocumentVersion documentVersion = documentService.GetVersion(document.id);
            string content = await documentService.GetFileContents(documentService.GetFile(document, documentVersion));
            return Ok(new {document, documentVersion, content});
        }

        [HttpPost("check"), Authorize]
        public async Task<IActionResult> Check([FromQuery] string directory, [FromQuery] string name) {
            return Ok(documentService.GetSingle(x => (string.IsNullOrEmpty(x.directory) || x.directory == directory) && string.Equals(x.name, name, StringComparison.InvariantCultureIgnoreCase)));
        }

        [HttpPut, Authorize]
        public async Task<IActionResult> CreateDocument([FromBody] Document document) {
            document.name = document.name.ToLower();
            document.directory = document.directory.ToLower();
            document.originalAuthor = sessionService.GetContextId();
            await documentService.Add(document);
            await documentService.UpdateUserPermissions(document.id, false, new[] {document.originalAuthor});
            await documentService.UpdateUserPermissions(document.id, true, new[] {document.originalAuthor});
            return Ok();
        }
    }
}
