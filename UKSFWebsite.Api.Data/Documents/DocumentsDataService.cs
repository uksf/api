using System.IO;
using System.Threading.Tasks;
using MongoDB.Driver;
using UKSFWebsite.Api.Interfaces.Data.Cached;
using UKSFWebsite.Api.Interfaces.Events;
using UKSFWebsite.Api.Models.Documents;
using UKSFWebsite.Api.Services.Admin;

namespace UKSFWebsite.Api.Data.Documents {
    public class DocumentsDataService : CachedDataService<Document, IDocumentsDataService>, IDocumentsDataService {
#if DEBUG
        private const string COLLECTION_NAME = "debugDocuments";
#else
        private const string COLLECTION_NAME = "documents";
#endif
        private const string DOCUMENTS_LOCATION = "DOCUMENTS_LOCATION";
        
        public DocumentsDataService(IMongoDatabase database, IDataEventBus<IDocumentsDataService> dataEventBus) : base(database, dataEventBus, COLLECTION_NAME) { }

        public override async Task Add(Document document) {
            Directory.CreateDirectory(Path.Combine(VariablesWrapper.VariablesDataService().GetSingle(DOCUMENTS_LOCATION).AsString(), document.directory, document.name));
            await base.Add(document);
        }

        public override async Task Delete(string id) {
            Document document = GetSingle(id);
            Directory.Delete(document.directory, true);
            await base.Delete(id);
        }
    }
}
