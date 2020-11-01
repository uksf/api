using UKSF.Api.Base.Extensions;

namespace UKSF.Api.Personnel.Services {
    public interface IObjectIdConversionService {
        string ConvertObjectIds(string text);
        string ConvertObjectId(string id);
    }

    public class ObjectIdConversionService : IObjectIdConversionService {
        private readonly IDisplayNameService displayNameService;
        private readonly IUnitsService unitsService;

        public ObjectIdConversionService(IDisplayNameService displayNameService, IUnitsService unitsService) {
            this.displayNameService = displayNameService;
            this.unitsService = unitsService;
        }

        public string ConvertObjectIds(string text) {
            if (string.IsNullOrEmpty(text)) return text;

            foreach (string objectId in text.ExtractObjectIds()) {
                string displayString = displayNameService.GetDisplayName(objectId);
                if (displayString == objectId) {
                    displayString = unitsService.Data.GetSingle(x => x.id == objectId)?.name;
                }

                text = text.Replace(objectId, displayString);
            }

            return text;
        }

        public string ConvertObjectId(string id) => string.IsNullOrEmpty(id) ? id : displayNameService.GetDisplayName(id);
    }
}
