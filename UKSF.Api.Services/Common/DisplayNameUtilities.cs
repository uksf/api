using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using UKSF.Api.Interfaces.Personnel;
using UKSF.Api.Interfaces.Units;
using UKSF.Api.Models.Units;

namespace UKSF.Api.Services.Common {
    public static class DisplayNameUtilities {
        public static string ConvertObjectIds(this string message) {
            string newMessage = message;
            if (!string.IsNullOrEmpty(message)) {
                IDisplayNameService displayNameService = ServiceWrapper.ServiceProvider.GetService<IDisplayNameService>();
                IUnitsService unitsService = ServiceWrapper.ServiceProvider.GetService<IUnitsService>();
                IEnumerable<string> parts = Regex.Split(message, @"\s+").Where(s => s != string.Empty);
                foreach (string part in parts) {
                    if (ObjectId.TryParse(part, out ObjectId _)) {
                        string displayName = displayNameService.GetDisplayName(part);
                        if (displayName == part) {
                            Unit unit = unitsService.Data().GetSingle(x => x.id == part);
                            if (unit != null) {
                                displayName = unit.name;
                            }
                        }

                        newMessage = newMessage.Replace(part, displayName);
                    }
                }
            }

            return newMessage;
        }
    }
}
