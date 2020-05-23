using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Interfaces.Personnel;
using UKSF.Api.Interfaces.Units;
using UKSF.Api.Models.Units;
using UKSF.Common;

namespace UKSF.Api.Services.Common {
    public static class DisplayNameUtilities {
        public static string ConvertObjectIds(this string message) {
            string newMessage = message;
            if (string.IsNullOrEmpty(message)) return newMessage;

            IDisplayNameService displayNameService = ServiceWrapper.ServiceProvider.GetService<IDisplayNameService>();
            IUnitsService unitsService = ServiceWrapper.ServiceProvider.GetService<IUnitsService>();
            List<string> objectIds = message.ExtractObjectIds().Where(s => s != string.Empty).ToList();
            foreach (string objectId in objectIds) {
                string displayString = displayNameService.GetDisplayName(objectId);
                if (displayString == objectId) {
                    Unit unit = unitsService.Data.GetSingle(x => x.id == objectId);
                    if (unit != null) {
                        displayString = unit.name;
                    }
                }

                newMessage = newMessage.Replace(objectId, displayString);
            }

            return newMessage;
        }
    }
}
