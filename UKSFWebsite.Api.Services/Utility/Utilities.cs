using System.Collections.Generic;
using System.Dynamic;
using System.Reflection;
using UKSFWebsite.Api.Models.Accounts;

namespace UKSFWebsite.Api.Services.Utility {
    public static class Utilities {
        public static dynamic ToDynamic<T>(this T obj) {
            IDictionary<string, object> expando = new ExpandoObject();

            foreach (PropertyInfo propertyInfo in typeof(T).GetProperties()) {
                object currentValue = propertyInfo.GetValue(obj);
                expando.Add(propertyInfo.Name, currentValue);
            }
            
            foreach (FieldInfo fieldInfo in typeof(T).GetFields()) {
                object currentValue = fieldInfo.GetValue(obj);
                expando.Add(fieldInfo.Name, currentValue);
            }

            return (ExpandoObject) expando;
        }

        public static dynamic ToDynamicAccount(this Account account) {
            dynamic dynamicAccount = account.ToDynamic();
            dynamicAccount.password = null;
            return dynamicAccount;
        }
    }
}
