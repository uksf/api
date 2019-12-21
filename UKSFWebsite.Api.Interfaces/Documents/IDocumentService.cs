using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UKSFWebsite.Api.Interfaces.Data.Cached;
using UKSFWebsite.Api.Models.Documents;

namespace UKSFWebsite.Api.Interfaces.Documents {
    public interface IDocumentService : IDataBackedService<IDocumentsDataService> {
        DocumentVersion GetVersion(string id, int version = -1);
        DocumentVersion GetVersion(Func<Document, bool> predicate, int version = -1);
        string GetFile(Document document, DocumentVersion documentVersion);
        Task<string> GetFileContents(string filePath);
        Task AddVersion(Document document, DocumentVersion newVersion);
        bool IdHasViewPermission(string documentId, string id);
        bool IdHasEditPermission(string documentId, string id);
        Task UpdateRankPermissions(string id, bool edit, string rank);
        Task UpdateUnitPermissions(string id, bool edit, IEnumerable<string> units);
        Task UpdateTrainingPermissions(string id, bool edit, IEnumerable<string> trainings);
        Task UpdateUserPermissions(string id, bool edit, IEnumerable<string> users);
    }
}
