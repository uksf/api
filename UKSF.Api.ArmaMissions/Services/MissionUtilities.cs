using System.Collections.Generic;
using System.IO;
using System.Linq;
using UKSF.Api.ArmaMissions.Models;
using UKSF.Api.Shared.Extensions;

namespace UKSF.Api.ArmaMissions.Services
{
    public static class MissionUtilities
    {
        public static List<string> ReadDataFromIndex(List<string> source, ref int index)
        {
            List<string> data = new() { source[index] };
            index += 1;
            var opening = source[index];
            Stack<string> stack = new();
            stack.Push(opening);
            data.Add(opening);
            index += 1;
            while (stack.Count != 0)
            {
                if (index >= source.Count)
                {
                    return new();
                }

                var line = source[index];
                if (line.Equals("{"))
                {
                    stack.Push(line);
                }

                if (line.Equals("};"))
                {
                    stack.Pop();
                }

                data.Add(line);
                index++;
            }

            return data;
        }

        public static int GetIndexByKey(List<string> source, string key)
        {
            var index = 0;
            while (true)
            {
                if (index >= source.Count)
                {
                    return -1;
                }

                var line = source[index];
                if (line.ToLower().Contains(key.ToLower()))
                {
                    return index;
                }

                index++;
            }
        }

        public static List<string> ReadDataByKey(List<string> source, string key)
        {
            var index = GetIndexByKey(source, key);
            return index == -1 ? new() : ReadDataFromIndex(source, ref index);
        }

        public static object ReadSingleDataByKey(List<string> source, string key)
        {
            var index = 0;
            while (true)
            {
                if (index >= source.Count)
                {
                    return "";
                }

                var line = source[index];
                var parts = line.Split('=');
                if (parts.Length == 2 && parts.First().Trim().ToLower().Equals(key.ToLower()))
                {
                    return parts.Last().Replace(";", "").Replace("\"", "").Trim();
                }

                index++;
            }
        }

        public static bool CheckFlag(Mission mission, string key)
        {
            mission.DescriptionLines = File.ReadAllLines(mission.DescriptionPath).ToList();
            return mission.DescriptionLines.Any(x => x.ContainsIgnoreCase(key));
        }
    }
}
