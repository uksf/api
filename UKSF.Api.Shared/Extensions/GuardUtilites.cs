using System;
using System.Linq;
using MongoDB.Bson;

namespace UKSF.Api.Shared.Extensions
{
    public static class GuardUtilites
    {
        public static void ValidateString(string text, Action<string> onInvalid)
        {
            if (string.IsNullOrEmpty(text))
            {
                onInvalid(text);
            }
        }

        public static void ValidateId(string id, Action<string> onInvalid)
        {
            if (string.IsNullOrEmpty(id))
            {
                onInvalid(id);
            }

            if (!ObjectId.TryParse(id, out var _))
            {
                onInvalid(id);
            }
        }

        public static void ValidateArray<T>(T[] array, Func<T[], bool> validate, Func<T, bool> elementValidate, Action onInvalid)
        {
            if (!validate(array))
            {
                onInvalid();
            }

            if (array.Any(x => !elementValidate(x)))
            {
                onInvalid();
            }
        }

        public static void ValidateIdArray(string[] array, Func<string[], bool> validate, Action onInvalid, Action<string> onIdInvalid)
        {
            if (!validate(array))
            {
                onInvalid();
            }

            Array.ForEach(array, x => ValidateId(x, onIdInvalid));
        }

        public static void ValidateTwoStrings(string first, string second, Action<string> onInvalid)
        {
            if (string.IsNullOrEmpty(first))
            {
                onInvalid(first);
            }

            if (string.IsNullOrEmpty(second))
            {
                onInvalid(second);
            }
        }
    }
}
