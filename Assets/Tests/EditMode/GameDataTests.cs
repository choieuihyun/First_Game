using Game.Data;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>실제 CSV 가 읽히는지, 틀린 데이터를 위치와 함께 잡아내는지 검사</summary>
    public class GameDataTests
    {
        [Test]
        public void RealData_LoadsAndValidates()
        {
            var d = TestData.Load();
            Assert.IsEmpty(GameDataValidator.Validate(d));
            Assert.AreEqual(5, d.Substances.Count);
            Assert.AreEqual(30f, d.Balance("tick_rate"));
        }

        [Test]
        public void FindReaction_IsOrderIndependent()
        {
            var d = TestData.Load();
            Assert.AreEqual("gas_explosion", d.FindReaction("fire", "gas").Id);
            Assert.AreEqual("gas_explosion", d.FindReaction("gas", "fire").Id);
            Assert.IsNull(d.FindReaction("fire", "lightning"));
            Assert.IsNull(d.FindReaction(null, "fire"));
        }

        [Test]
        public void Effect_StepsAreOrdered()
        {
            var fx = TestData.Load().Effect("fx_electrolysis");
            Assert.AreEqual(2, fx.Steps.Count);
            Assert.AreEqual(EffectPiece.Chain, fx.Steps[0].Piece);
            Assert.AreEqual(3, fx.Steps[0].Count);
            Assert.AreEqual(EffectPiece.SpawnZone, fx.Steps[1].Piece);
            Assert.AreEqual("hydrogen", fx.Steps[1].Substance);
        }

        [Test]
        public void Stage_ParsesEnemyWeights()
        {
            var s = TestData.Load().Stage("swamp");
            Assert.AreEqual(2, s.Enemies.Count);
            Assert.AreEqual("grunt", s.Enemies[0].Key);
            Assert.AreEqual(2f, s.Enemies[0].Value);
            Assert.AreEqual("soaked", s.Enemies[1].Key);
            Assert.AreEqual(1f, s.Enemies[1].Value);
            Assert.AreEqual(7, s.Runes.Count);
        }

        [Test]
        public void Delivery_And_Enemy_AreParsed()
        {
            var d = TestData.Load();
            Assert.AreEqual(DeliveryKind.ZoneAtTarget, d.Delivery("cloud").Kind);
            Assert.IsTrue(d.IsDelivery("bolt"));
            Assert.IsFalse(d.IsDelivery("fire"));
            Assert.IsNull(d.Enemy("grunt").Substance);
            Assert.AreEqual("water", d.Enemy("soaked").Substance);
        }

        [Test]
        public void UnknownSubstanceInReaction_Throws()
        {
            var ex = Assert.Throws<DataException>(() => TestData.LoadWith("reactions",
                "id,name,a,b,priority,effect\nx,엑스,gas,lava,10,fx_gas_explosion\n"));
            Assert.That(ex.Message, Does.Contain("reactions").And.Contain("lava"));
        }

        [Test]
        public void DuplicateId_Throws()
        {
            var ex = Assert.Throws<DataException>(() => TestData.LoadWith("enemies",
                "id,name,hp,speed,radius,contact_dps,substance,color,shape\n" +
                "grunt,가,20,1,0.3,1,,#FFFFFF,square\ngrunt,나,20,1,0.3,1,,#FFFFFF,square\nsoaked,다,20,1,0.3,1,,#FFFFFF,square\n"));
            Assert.That(ex.Message, Does.Contain("enemies").And.Contain("grunt"));
        }

        [Test]
        public void SubstancesSharingShape_Throws()
        {
            string text = TestData.ReadCsv("substances").Replace("drop", "triangle");
            var ex = Assert.Throws<DataException>(() => TestData.LoadWith("substances", text));
            Assert.That(ex.Message, Does.Contain("triangle"));
        }

        [Test]
        public void SamePairSamePriority_Throws()
        {
            string text = TestData.ReadCsv("reactions").TrimEnd() + "\ndup,중복,fire,gas,10,fx_gas_explosion\n";
            Assert.Throws<DataException>(() => TestData.LoadWith("reactions", text));
        }

        [Test]
        public void MissingBalanceKey_Throws()
        {
            var ex = Assert.Throws<DataException>(() => TestData.LoadWith("balance", "key,value,note\ntick_rate,30,\n"));
            Assert.That(ex.Message, Does.Contain("chain_depth_limit"));
        }

        [Test]
        public void NonRuneSubstanceInStageRunes_Throws()
        {
            string text = TestData.ReadCsv("stages").Replace("lightning,grunt", "lightning|hydrogen,grunt");
            var ex = Assert.Throws<DataException>(() => TestData.LoadWith("stages", text));
            Assert.That(ex.Message, Does.Contain("hydrogen"));
        }

        [Test]
        public void EmptyRequiredNumber_Throws()
        {
            var ex = Assert.Throws<DataException>(() => TestData.LoadWith("enemies",
                "id,name,hp,speed,radius,contact_dps,substance,color,shape\ngrunt,가,,1,0.3,1,,#FFFFFF,square\n"));
            Assert.That(ex.Message, Does.Contain("enemies").And.Contain("hp"));
        }

        [Test]
        public void BadColor_Throws()
        {
            string text = TestData.ReadCsv("substances").Replace("#FF7326", "orange");
            Assert.Throws<DataException>(() => TestData.LoadWith("substances", text));
        }

        [Test]
        public void UnknownEffectPiece_Throws()
        {
            string text = TestData.ReadCsv("effects").Replace("explode,40", "kaboom,40");
            var ex = Assert.Throws<DataException>(() => TestData.LoadWith("effects", text));
            Assert.That(ex.Message, Does.Contain("kaboom"));
        }

        [Test]
        public void RealCsvFiles_StartWithBom_SoExcelShowsKorean()
        {
            foreach (string name in GameData.FileNames)
            {
                byte[] bytes = TestData.ReadCsvBytes(name);
                Assert.IsTrue(bytes.Length > 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF, name);
            }
        }

        [Test]
        public void UnknownStartDelivery_Throws()
        {
            string text = TestData.ReadCsv("stages").TrimEnd();
            text = text.Substring(0, text.Length - 4) + "nope\n";
            var ex = Assert.Throws<DataException>(() => TestData.LoadWith("stages", text));
            Assert.That(ex.Message, Does.Contain("stages").And.Contain("nope"));
        }

        [Test]
        public void ZeroCaps_Throw()
        {
            foreach (string key in new[] { "max_enemies", "max_zones", "max_projectiles", "max_reactions_per_tick", "chain_depth_limit" })
            {
                var ex = Assert.Throws<DataException>(() => TestData.LoadWithBalance(key, "0"), key);
                Assert.That(ex.Message, Does.Contain(key));
            }
        }

        [Test]
        public void DeliveryAndSubstanceSharingId_Throws()
        {
            string text = TestData.ReadCsv("deliveries").TrimEnd() + "\nfire,불탄,bolt,1,1,1,0.2,5,0\n";
            var ex = Assert.Throws<DataException>(() => TestData.LoadWith("deliveries", text));
            Assert.That(ex.Message, Does.Contain("fire"));
        }
    }
}
