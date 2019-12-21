using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Driver;
using UKSFWebsite.Api.Models.Documents;
using UKSFWebsite.Api.Services.Abstraction;
using UKSFWebsite.Api.Services.Utility;

namespace UKSFWebsite.Api.Services.Data.Documents {
    public class DocumentService : CachedDataService<Document>, IDocumentService {
#if DEBUG
        private const string COLLECTION_NAME = "debugDocuments";
#else
        private const string COLLECTION_NAME = "documents";
#endif
        private const string DOCUMENTS_LOCATION = "DOCUMENTS_LOCATION";

        private readonly IDocumentPermissionService documentPermissionService;

        public DocumentService(IMongoDatabase database, IDocumentPermissionService documentPermissionService) : base(database, COLLECTION_NAME) => this.documentPermissionService = documentPermissionService;

        public DocumentVersion GetVersion(string id, int version = -1) {
            Document document = GetSingle(id);
            return version > -1 ? document.versions.FirstOrDefault(x => x.versionNumber == version) : document.versions.OrderByDescending(x => x.versionNumber).First();
        }

        public DocumentVersion GetVersion(Func<Document, bool> predicate, int version = -1) {
            Document document = GetSingle(predicate);
            return version > -1 ? document.versions.FirstOrDefault(x => x.versionNumber == version) : document.versions.OrderByDescending(x => x.versionNumber).First();
        }

        public string GetFile(Document document, DocumentVersion documentVersion) => Path.Combine(VariablesWrapper.VariablesService().GetSingle(DOCUMENTS_LOCATION).AsString(), document.directory, documentVersion.versionFile);

        public async Task<string> GetFileContents(string filePath) => await File.ReadAllTextAsync(filePath);

        public override async Task Add(Document document) {
            Directory.CreateDirectory(Path.Combine(VariablesWrapper.VariablesService().GetSingle(DOCUMENTS_LOCATION).AsString(), document.directory, document.name));
            await base.Add(document);
        }

        public async Task AddVersion(Document document, DocumentVersion newVersion) {
            newVersion.versionNumber = document.versions.Count;
            newVersion.versionFile = $"{document.name}_{newVersion.versionNumber}";
            await Update(document.id, Builders<Document>.Update.Push(x => x.versions, newVersion));
        }

        public bool IdHasViewPermission(string documentId, string id) {
            Document document = GetSingle(documentId);
            return documentPermissionService.HasPermission(document.viewPermissions, id);
        }

        public bool IdHasEditPermission(string documentId, string id) {
            Document document = GetSingle(documentId);
            return documentPermissionService.HasPermission(document.editPermissions, id);
        }

        public async Task UpdateRankPermissions(string id, bool edit, string rank) {
            Document document = GetSingle(id);
            documentPermissionService.UpdateRankPermissions(edit ? document.editPermissions : document.viewPermissions, rank);
            await UpdatePermissions(id, document, edit);
        }

        public async Task UpdateUnitPermissions(string id, bool edit, IEnumerable<string> units) {
            Document document = GetSingle(id);
            documentPermissionService.UpdateUnitPermissions(edit ? document.editPermissions : document.viewPermissions, units);
            await UpdatePermissions(id, document, edit);
        }

        public async Task UpdateTrainingPermissions(string id, bool edit, IEnumerable<string> trainings) {
            Document document = GetSingle(id);
            documentPermissionService.UpdateTrainingPermissions(edit ? document.editPermissions : document.viewPermissions, trainings);
            await UpdatePermissions(id, document, edit);
        }

        public async Task UpdateUserPermissions(string id, bool edit, IEnumerable<string> users) {
            Document document = GetSingle(id);
            documentPermissionService.UpdateUserPermissions(edit ? document.editPermissions : document.viewPermissions, users);
            await UpdatePermissions(id, document, edit);
        }

        private async Task UpdatePermissions(string id, Document document, bool edit) {
            if (edit) {
                await Update(id, Builders<Document>.Update.Set(x => x.editPermissions, document.editPermissions));
            } else {
                await Update(id, Builders<Document>.Update.Set(x => x.viewPermissions, document.viewPermissions));
            }
        }

        public override async Task Delete(string id) {
            Document document = GetSingle(id);
            Directory.Delete(document.directory, true);
            await base.Delete(id);
        }
    }
}
