using System.Collections.Generic;
using System.Linq;

namespace UKSF.Api.Services.Game.Missions {
    public static class MissionUtilities {
        public static List<string> ReadDataFromIndex(List<string> source, ref int index) {
            List<string> data = new List<string> {source[index]};
            index += 1;
            string opening = source[index];
            Stack<string> stack = new Stack<string>();
            stack.Push(opening);
            data.Add(opening);
            index += 1;
            while (stack.Count != 0) {
                if (index >= source.Count) return new List<string>();
                string line = source[index];
                if (line.Equals("{")) {
                    stack.Push(line);
                }

                if (line.Equals("};")) {
                    stack.Pop();
                }

                data.Add(line);
                index++;
            }

            return data;
        }

        public static int GetIndexByKey(List<string> source, string key) {
            int index = 0;
            while (true) {
                if (index >= source.Count) return -1;
                string line = source[index];
                if (line.ToLower().Contains(key.ToLower())) {
                    return index;
                }

                index++;
            }
        }

        public static List<string> ReadDataByKey(List<string> source, string key) {
            int index = GetIndexByKey(source, key);
            return index == -1 ? new List<string>() : ReadDataFromIndex(source, ref index);
        }

        public static object ReadSingleDataByKey(List<string> source, string key) {
            int index = 0;
            while (true) {
                if (index >= source.Count) return "";
                string line = source[index];
                string[] parts = line.Split('=');
                if (parts.Length == 2 && parts.First().ToLower().Equals(key.ToLower())) {
                    return parts.Last().Replace(";", "").Replace("\"", "").Trim();
                }

                index++;
            }
        }
    }
}
