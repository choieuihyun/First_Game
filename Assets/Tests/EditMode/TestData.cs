using System;
using System.IO;
using Game.Data;

namespace Game.Tests
{
    /// <summary>테스트에서 실제 CSV 를 읽는 도우미. dotnet 과 Unity 양쪽에서 저장소 폴더를 찾아낸다</summary>
    public static class TestData
    {
        static string dataDir;

        static string DataDir()
        {
            if (dataDir != null) return dataDir;
            foreach (string start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
            {
                var dir = new DirectoryInfo(start);
                while (dir != null)
                {
                    string candidate = Path.Combine(dir.FullName, "Assets", "Resources", "Data");
                    if (Directory.Exists(candidate)) { dataDir = candidate; return dataDir; }
                    dir = dir.Parent;
                }
            }
            throw new DirectoryNotFoundException("Assets/Resources/Data 폴더를 찾지 못했습니다");
        }

        /// <summary>확장자 없는 이름으로 CSV 글자를 읽는다</summary>
        public static string ReadCsv(string name)
        {
            return File.ReadAllText(Path.Combine(DataDir(), name + ".csv"));
        }

        /// <summary>파일의 바이트를 그대로 읽는다</summary>
        public static byte[] ReadCsvBytes(string name)
        {
            return File.ReadAllBytes(Path.Combine(DataDir(), name + ".csv"));
        }

        /// <summary>실제 데이터를 그대로 읽는다</summary>
        public static GameData Load()
        {
            return GameData.Load(ReadCsv);
        }

        /// <summary>파일 하나만 주어진 글자로 바꿔서 읽는다</summary>
        public static GameData LoadWith(string name, string replacedText)
        {
            return GameData.Load(n => n == name ? replacedText : ReadCsv(n));
        }

        /// <summary>balance.csv 의 값 하나만 바꿔서 읽는다</summary>
        public static GameData LoadWithBalance(string key, string value)
        {
            string[] lines = ReadCsv("balance").Replace("\r", "").Split('\n');
            bool found = false;
            for (int i = 0; i < lines.Length; i++)
            {
                if (!lines[i].StartsWith(key + ",")) continue;
                string[] parts = lines[i].Split(',');
                parts[1] = value;
                lines[i] = string.Join(",", parts);
                found = true;
            }
            if (!found) throw new ArgumentException("balance 에 없는 키: " + key);
            return LoadWith("balance", string.Join("\n", lines));
        }
    }
}
