using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Shared.Extensions;

namespace UKSF.Api.Personnel.Services {
    public interface IObjectIdConversionService {
        string ConvertObjectIds(string text);
        string ConvertObjectId(string id);
    }

    public class ObjectIdConversionService : IObjectIdConversionService {
        private readonly IDisplayNameService _displayNameService;
        private readonly IUnitsContext _unitsContext;

        public ObjectIdConversionService(IUnitsContext unitsContext, IDisplayNameService displayNameService) {
            _unitsContext = unitsContext;
            _displayNameService = displayNameService;
        }

        public string ConvertObjectIds(string text) {
            if (string.IsNullOrEmpty(text)) return text;

            foreach (string objectId in text.ExtractObjectIds()) {
                string displayString = _displayNameService.GetDisplayName(objectId);
                if (displayString == objectId) {
                    Unit unit = _unitsContext.GetSingle(x => x.Id == objectId);
                    if (unit != null) {
                        displayString = unit.Name;
                    }
                }

                text = text.Replace(objectId, displayString);
            }

            return text;
        }

        public string ConvertObjectId(string id) => string.IsNullOrEmpty(id) ? id : _displayNameService.GetDisplayName(id);
    }
}
