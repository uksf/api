using System;
using System.Reflection;

namespace UKSF.Api.Shared.Extensions {
    public static class ObjectExtensions {
        public static object GetFieldValue(this object obj, string fieldName) {
            if (obj == null) throw new ArgumentNullException(nameof(obj));
            Type objType = obj.GetType();
            FieldInfo fieldInfo = GetFieldInfo(objType, fieldName);
            if (fieldInfo == null) throw new ArgumentOutOfRangeException(fieldName, $"Couldn't find field {fieldName} in type {objType.FullName}");
            return fieldInfo.GetValue(obj);
        }

        private static FieldInfo GetFieldInfo(Type type, string fieldName) {
            FieldInfo fieldInfo;
            do {
                fieldInfo = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                type = type.BaseType;
            } while (fieldInfo == null && type != null);

            return fieldInfo;
        }

        public static object GetPropertyValue(this object obj, string propertyName) {
            if (obj == null) throw new ArgumentNullException(nameof(obj));
            Type objType = obj.GetType();
            PropertyInfo propertyInfo = GetPropertyInfo(objType, propertyName);
            if (propertyInfo == null) throw new ArgumentOutOfRangeException(propertyName, $"Couldn't find property {propertyName} in type {objType.FullName}");
            return propertyInfo.GetValue(obj, null);
        }

        private static PropertyInfo GetPropertyInfo(Type type, string propertyName) {
            PropertyInfo propertyInfo;
            do {
                propertyInfo = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                type = type.BaseType;
            } while (propertyInfo == null && type != null);

            return propertyInfo;
        }
    }
}
