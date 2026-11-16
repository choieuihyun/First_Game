using System;

namespace Game.Data
{
    /// <summary>CSV 데이터가 잘못됐을 때 던진다. 어느 파일 몇 행 무슨 열인지 메시지에 담는다</summary>
    public sealed class DataException : Exception
    {
        /// <summary>row 는 엑셀에서 보이는 행 번호다. 머리글이 1행</summary>
        public DataException(string file, int row, string column, string message)
            : base(file + " " + row + "행 " + column + "열: " + message)
        {
        }

        /// <summary>특정 칸을 짚을 수 없는 문제</summary>
        public DataException(string file, string message)
            : base(file + ": " + message)
        {
        }

        /// <summary>이미 완성된 문장을 그대로 쓴다</summary>
        public DataException(string message)
            : base(message)
        {
        }
    }
}
