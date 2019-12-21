using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Driver;
using UKSFWebsite.Api.Interfaces.Data.Cached;
using UKSFWebsite.Api.Interfaces.Documents;
using UKSFWebsite.Api.Models.Documents;
using UKSFWebsite.Api.Services.Abstraction;
using UKSFWebsite.Api.Services.Admin;

namespace UKSFWebsite.Api.Services.Documents {
    public class DocumentService : IDocumentService {
        private const string DOCUMENTS_LOCATION = "DOCUMENTS_LOCATION";

        private readonly IDocumentsDataService data;
        private readonly IDocumentPermissionService documentPermissionService;

        public DocumentService(IDocumentsDataService data, IDocumentPermissionService documentPermissionService) {
            this.data = data;
            this.documentPermissionService = documentPermissionService;
        }

        public IDocumentsDataService Data() => data;

        public DocumentVersion GetVersion(string id, int version = -1) {
            Document document = Data().GetSingle(id);
            return version > -1 ? document.versions.FirstOrDefault(x => x.versionNumber == version) : document.versions.OrderByDescending(x => x.versionNumber).First();
        }

        public DocumentVersion GetVersion(Func<Document, bool> predicate, int version = -1) {
            Document document = Data().GetSingle(predicate);
            return version > -1 ? document.versions.FirstOrDefault(x => x.versionNumber == version) : document.versions.OrderByDescending(x => x.versionNumber).First();
        }

        public string GetFile(Document document, DocumentVersion documentVersion) => Path.Combine(VariablesWrapper.VariablesDataService().GetSingle(DOCUMENTS_LOCATION).AsString(), document.directory, documentVersion.versionFile);

        public async Task<string> GetFileContents(string filePath) => await File.ReadAllTextAsync(filePath);

        public async Task AddVersion(Document document, DocumentVersion newVersion) {
            newVersion.versionNumber = document.versions.Count;
            newVersion.versionFile = $"{document.name}_{newVersion.versionNumber}";
            await Data().Update(document.id, Builders<Document>.Update.Push(x => x.versions, newVersion));
        }

        public bool IdHasViewPermission(string documentId, string id) {
            Document document = Data().GetSingle(documentId);
            return documentPermissionService.HasPermission(document.viewPermissions, id);
        }

        public bool IdHasEditPermission(string documentId, string id) {
            Document document = Data().GetSingle(documentId);
            return documentPermissionService.HasPermission(document.editPermissions, id);
        }

        public async Task UpdateRankPermissions(string id, bool edit, string rank) {
            Document document = Data().GetSingle(id);
            documentPermissionService.UpdateRankPermissions(edit ? document.editPermissions : document.viewPermissions, rank);
            await UpdatePermissions(id, document, edit);
        }

        public async Task UpdateUnitPermissions(string id, bool edit, IEnumerable<string> units) {
            Document document = Data().GetSingle(id);
            documentPermissionService.UpdateUnitPermissions(edit ? document.editPermissions : document.viewPermissions, units);
            await UpdatePermissions(id, document, edit);
        }

        public async Task UpdateTrainingPermissions(string id, bool edit, IEnumerable<string> trainings) {
            Document document = Data().GetSingle(id);
            documentPermissionService.UpdateTrainingPermissions(edit ? document.editPermissions : document.viewPermissions, trainings);
            await UpdatePermissions(id, document, edit);
        }

        public async Task UpdateUserPermissions(string id, bool edit, IEnumerable<string> users) {
            Document document = Data().GetSingle(id);
            documentPermissionService.UpdateUserPermissions(edit ? document.editPermissions : document.viewPermissions, users);
            await UpdatePermissions(id, document, edit);
        }

        private async Task UpdatePermissions(string id, Document document, bool edit) {
            await Data().Update(id, Builders<Document>.Update.Set(x => edit ? x.editPermissions : x.viewPermissions, edit ? document.editPermissions : document.viewPermissions));
        }
    }
}
