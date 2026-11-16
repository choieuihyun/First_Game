using Game.Data;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>CSV 읽기 검사. 엑셀이나 구글 시트에서 다시 저장한 파일도 읽혀야 한다</summary>
    public class CsvTableTests
    {
        [Test]
        public void Parses_BomCrlfQuotesAndBlankLines()
        {
            var t = CsvTable.Parse("x.csv",
                "﻿id,name,note\r\nfire,\"불, 뜨거움\",\"그는 \"\"앗\"\" 했다\"\r\n\r\nwater, 물 ,\r\n\r\n");
            Assert.AreEqual(2, t.RowCount);
            Assert.AreEqual("불, 뜨거움", t.Get(0, "name"));
            Assert.AreEqual("그는 \"앗\" 했다", t.Get(0, "note"));
            Assert.AreEqual("물", t.Get(1, "name"));
            Assert.AreEqual("", t.Get(1, "note"));
        }

        [Test]
        public void GetFloat_UsesDotDecimal()
        {
            var t = CsvTable.Parse("x.csv", "id,v\na,0.5\n");
            Assert.AreEqual(0.5f, t.GetFloat(0, "v"));
        }

        [Test]
        public void GetFloat_NotNumber_ThrowsWithLocation()
        {
            var t = CsvTable.Parse("enemies.csv", "id,hp\ngrunt,abc\n");
            var ex = Assert.Throws<DataException>(() => t.GetFloat(0, "hp"));
            Assert.That(ex.Message, Does.Contain("enemies.csv").And.Contain("2행").And.Contain("hp"));
        }

        [Test]
        public void GetFloat_Empty_Throws_ButGetFloatOr_ReturnsFallback()
        {
            var t = CsvTable.Parse("x.csv", "id,v\na,\n");
            Assert.Throws<DataException>(() => t.GetFloat(0, "v"));
            Assert.AreEqual(7f, t.GetFloatOr(0, "v", 7f));
        }

        [Test]
        public void MissingColumn_Throws()
        {
            var t = CsvTable.Parse("x.csv", "id,v\na,1\n");
            var ex = Assert.Throws<DataException>(() => t.Get(0, "nope"));
            Assert.That(ex.Message, Does.Contain("nope"));
        }

        [Test]
        public void RowWithWrongFieldCount_Throws()
        {
            var ex = Assert.Throws<DataException>(() => CsvTable.Parse("x.csv", "id,a,b\n1,2,3\n4,5\n"));
            Assert.That(ex.Message, Does.Contain("3행"));
        }

        [Test]
        public void DuplicateHeader_Throws()
        {
            Assert.Throws<DataException>(() => CsvTable.Parse("x.csv", "id,id\n1,2\n"));
        }

        [Test]
        public void GetList_SplitsOnPipe()
        {
            var t = CsvTable.Parse("x.csv", "id,list,none\nx,a|b| c,\n");
            Assert.AreEqual(new[] { "a", "b", "c" }, t.GetList(0, "list"));
            Assert.AreEqual(0, t.GetList(0, "none").Length);
        }

        [Test]
        public void GetBool_ReadsTrueFalse_AndRejectsOthers()
        {
            var t = CsvTable.Parse("x.csv", "id,a,b,c\nx,true,FALSE,yes\n");
            Assert.IsTrue(t.GetBool(0, "a"));
            Assert.IsFalse(t.GetBool(0, "b"));
            Assert.Throws<DataException>(() => t.GetBool(0, "c"));
        }

        [Test]
        public void QuotedNewline_StaysInValue()
        {
            var t = CsvTable.Parse("x.csv", "id,note\nx,\"첫 줄\n둘째 줄\"\n");
            Assert.AreEqual(1, t.RowCount);
            Assert.AreEqual("첫 줄\n둘째 줄", t.Get(0, "note"));
        }

        [Test]
        public void TrailingEmptyColumns_FromExcel_AreIgnored()
        {
            var t = CsvTable.Parse("x.csv", "key,value,note,,\ntick_rate,30,설명,,\n");
            Assert.AreEqual(1, t.RowCount);
            Assert.AreEqual("30", t.Get(0, "value"));
            Assert.IsFalse(t.HasColumn(""));
        }

        [Test]
        public void CrOnlyLineEndings_AreRows()
        {
            var t = CsvTable.Parse("x.csv", "id,v\ra,1\rb,2\r");
            Assert.AreEqual(2, t.RowCount);
            Assert.AreEqual("2", t.Get(1, "v"));
        }

        [Test]
        public void NanAndInfinity_AreRejected()
        {
            var t = CsvTable.Parse("x.csv", "id,v,w\na,NaN,Infinity\n");
            Assert.Throws<DataException>(() => t.GetFloat(0, "v"));
            Assert.Throws<DataException>(() => t.GetFloat(0, "w"));
        }
    }
}
