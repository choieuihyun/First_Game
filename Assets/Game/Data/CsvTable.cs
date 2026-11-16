using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Game.Data
{
    /// <summary>CSV 한 파일을 읽어 행과 열 이름으로 값을 꺼내게 해 준다</summary>
    public sealed class CsvTable
    {
        readonly List<string> columns;
        readonly Dictionary<string, int> columnIndex = new Dictionary<string, int>();
        readonly List<string[]> rows = new List<string[]>();
        // 각 행이 엑셀에서 몇 번째 행으로 보이는지. 에러 메시지에 쓴다
        readonly List<int> sheetRows = new List<int>();

        /// <summary>에러 메시지에 넣을 파일 이름</summary>
        public string FileName { get; }

        /// <summary>머리글과 빈 줄을 뺀 행 수</summary>
        public int RowCount => rows.Count;

        CsvTable(string fileName, List<string> columns)
        {
            FileName = fileName;
            this.columns = columns;
        }

        /// <summary>CSV 글자를 읽어 표로 만든다. 첫 줄은 열 이름이다</summary>
        public static CsvTable Parse(string fileName, string text)
        {
            if (text == null) throw new DataException(fileName, "내용이 없습니다");
            // 엑셀이 붙이는 BOM 을 뗀다
            if (text.Length > 0 && text[0] == '﻿') text = text.Substring(1);

            List<List<string>> records = SplitRecords(fileName, text);
            if (records.Count == 0 || IsBlank(records[0])) throw new DataException(fileName, "머리글 행이 없습니다");

            var header = new List<string>();
            foreach (string h in records[0]) header.Add(h.Trim());
            // 엑셀이 끝에 붙이는 빈 열은 버린다
            while (header.Count > 0 && header[header.Count - 1].Length == 0) header.RemoveAt(header.Count - 1);
            var table = new CsvTable(fileName, header);
            for (int i = 0; i < header.Count; i++)
            {
                if (header[i].Length == 0) throw new DataException(fileName, 1, (i + 1) + "번째", "열 이름이 비어 있습니다");
                if (table.columnIndex.ContainsKey(header[i])) throw new DataException(fileName, 1, header[i], "열 이름이 겹칩니다");
                table.columnIndex[header[i]] = i;
            }

            for (int r = 1; r < records.Count; r++)
            {
                List<string> rec = records[r];
                if (IsBlank(rec)) continue;
                int sheetRow = r + 1;
                while (rec.Count > header.Count && rec[rec.Count - 1].Trim().Length == 0) rec.RemoveAt(rec.Count - 1);
                if (rec.Count != header.Count)
                    throw new DataException(fileName, sheetRow, "전체",
                        "칸 수가 " + rec.Count + "개입니다. 머리글은 " + header.Count + "개입니다");
                var values = new string[rec.Count];
                for (int c = 0; c < rec.Count; c++) values[c] = rec[c].Trim();
                table.rows.Add(values);
                table.sheetRows.Add(sheetRow);
            }
            return table;
        }

        static bool IsBlank(List<string> record)
        {
            foreach (string f in record)
                if (f.Trim().Length > 0) return false;
            return true;
        }

        /// <summary>따옴표 상태를 따라가며 글자 단위로 나눈다. 따옴표 안의 쉼표와 줄바꿈은 값이다</summary>
        static List<List<string>> SplitRecords(string fileName, string text)
        {
            var records = new List<List<string>>();
            var record = new List<string>();
            var field = new StringBuilder();
            bool inQuotes = false;
            bool recordHasContent = false;

            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if (inQuotes)
                {
                    if (ch == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                        else inQuotes = false;
                    }
                    else field.Append(ch);
                    continue;
                }

                if (ch == '"') { inQuotes = true; recordHasContent = true; }
                else if (ch == ',') { record.Add(field.ToString()); field.Length = 0; recordHasContent = true; }
                else if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n') { /* CRLF 의 CR 은 버린다 */ }
                else if (ch == '\n' || ch == '\r')
                {
                    // CR 만 쓰는 옛 형식도 줄바꿈으로 본다
                    record.Add(field.ToString());
                    field.Length = 0;
                    records.Add(record);
                    record = new List<string>();
                    recordHasContent = false;
                }
                else { field.Append(ch); recordHasContent = true; }
            }

            if (inQuotes) throw new DataException(fileName, "따옴표가 닫히지 않았습니다");
            // 마지막 줄이 줄바꿈 없이 끝난 경우
            if (recordHasContent || field.Length > 0)
            {
                record.Add(field.ToString());
                records.Add(record);
            }
            return records;
        }

        /// <summary>그 이름의 열이 있는지</summary>
        public bool HasColumn(string column) => columnIndex.ContainsKey(column);

        /// <summary>값을 글자로 꺼낸다. 앞뒤 공백은 뗀다. 빈 칸은 빈 글자</summary>
        public string Get(int row, string column)
        {
            if (!columnIndex.TryGetValue(column, out int c))
                throw new DataException(FileName, 1, column, "이 열이 없습니다");
            return rows[row][c];
        }

        /// <summary>값을 숫자로 꺼낸다. 빈 칸이거나 숫자가 아니면 에러</summary>
        public float GetFloat(int row, string column)
        {
            string s = Get(row, column);
            if (s.Length == 0) throw Error(row, column, "값이 비어 있습니다");
            if (!float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
                throw Error(row, column, "숫자가 아닙니다: " + s);
            if (float.IsNaN(v) || float.IsInfinity(v)) throw Error(row, column, "쓸 수 없는 숫자입니다: " + s);
            return v;
        }

        /// <summary>값을 숫자로 꺼낸다. 빈 칸이면 fallback</summary>
        public float GetFloatOr(int row, string column, float fallback)
        {
            return Get(row, column).Length == 0 ? fallback : GetFloat(row, column);
        }

        /// <summary>true 또는 false 를 읽는다</summary>
        public bool GetBool(int row, string column)
        {
            string s = Get(row, column);
            if (string.Equals(s, "true", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(s, "false", StringComparison.OrdinalIgnoreCase)) return false;
            throw Error(row, column, "true 또는 false 여야 합니다: " + s);
        }

        /// <summary>세로 막대로 나눈 목록을 읽는다. 빈 칸이면 빈 배열</summary>
        public string[] GetList(int row, string column)
        {
            string s = Get(row, column);
            if (s.Length == 0) return new string[0];
            string[] parts = s.Split('|');
            for (int i = 0; i < parts.Length; i++) parts[i] = parts[i].Trim();
            return parts;
        }

        /// <summary>이 표의 특정 칸을 짚는 에러를 만든다</summary>
        public DataException Error(int row, string column, string message)
        {
            return new DataException(FileName, sheetRows[row], column, message);
        }
    }
}
