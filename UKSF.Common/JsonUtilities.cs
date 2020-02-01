using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace UKSF.Common {
    public static class JsonUtilities {
        public static string DeepJsonSerializeObject(this object data) => JsonConvert.SerializeObject(data, new JsonSerializerSettings {ContractResolver = new AllFieldsContractResolver()});

        public static T Copy<T>(this object source) {
            JsonSerializerSettings deserializeSettings = new JsonSerializerSettings {ObjectCreationHandling = ObjectCreationHandling.Replace};
            return JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(source), deserializeSettings);
        }

        private class AllFieldsContractResolver : DefaultContractResolver {
            protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization) {
                List<JsonProperty> jsonProperties = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                                               .Select(x => base.CreateProperty(x, memberSerialization))
                                               .Union(type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Select(x => base.CreateProperty(x, memberSerialization)))
                                               .ToList();
                jsonProperties.ForEach(
                    x => {
                        x.Writable = true;
                        x.Readable = true;
                    }
                );
                return jsonProperties;
            }
        }
    }
}
