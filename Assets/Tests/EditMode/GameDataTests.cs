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
            Assert.AreEqual(10, d.Substances.Count);
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
        public void Stage_ParsesEnemiesAndAbsorbList()
        {
            var s = TestData.Load().Stage("swamp");
            Assert.AreEqual(10, s.Enemies.Count);
            Assert.AreEqual("slime", s.Enemies[0].Body);
            Assert.AreEqual("water", s.Enemies[0].Substance);
            Assert.AreEqual(5f, s.Enemies[0].Weight);
            Assert.AreEqual("slime", s.Enemies[2].Body);
            Assert.IsNull(s.Enemies[2].Substance);
            Assert.AreEqual(8, s.Absorb.Count);
            Assert.AreEqual("pool", s.Absorb[0].Key);
            Assert.AreEqual(10f, s.Absorb[0].Value);
            Assert.AreEqual(2, s.AbsorbIndex("cloud"));
            Assert.AreEqual(-1, s.AbsorbIndex("hydrogen"));
            Assert.AreEqual(-1, s.AbsorbIndex(null));
        }

        [Test]
        public void Delivery_And_Body_AreParsed()
        {
            var d = TestData.Load();
            Assert.AreEqual(DeliveryKind.Zone, d.Delivery("cloud").Kind);
            Assert.AreEqual(DeliveryKind.Bolt, d.Delivery("bolt").Kind);
            Assert.IsTrue(d.IsDelivery("bolt"));
            Assert.IsFalse(d.IsDelivery("fire"));
            Assert.AreEqual("bolt", d.Enemy("horn").Delivery);
            Assert.AreEqual("slime", d.BodyForDelivery("pool").Id);
            Assert.IsNull(d.BodyForDelivery("nope"));
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
                "id,name,delivery,behavior,hp,speed,radius,contact_dps,color,shape\n" +
                "horn,가,bolt,chase,20,1,0.3,1,#FFFFFF,square\nhorn,나,bolt,chase,20,1,0.3,1,#FFFFFF,square\n"));
            Assert.That(ex.Message, Does.Contain("enemies").And.Contain("horn"));
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
        public void NonAbsorbableSubstanceInAbsorbList_Throws()
        {
            string text = TestData.ReadCsv("stages").Replace("lightning:5,", "lightning:5|hydrogen:5,");
            var ex = Assert.Throws<DataException>(() => TestData.LoadWith("stages", text));
            Assert.That(ex.Message, Does.Contain("hydrogen"));
        }

        [Test]
        public void TraitNoEnemyCarries_Throws()
        {
            // 흡수 목록에 불이 있는데 불을 지닌 적이 나오지 않으면 얻을 수 없는 특징이다
            string text = "id,name,width,height,absorb,enemies,terrain_substance,terrain_count,terrain_radius,start_delivery\n" +
                          "only,하나,40,40,bolt:5|fire:5,horn::1,,0,0,bolt\n";
            var ex = Assert.Throws<DataException>(() => TestData.LoadWith("stages", text));
            Assert.That(ex.Message, Does.Contain("fire"));
        }

        [Test]
        public void ThreeStages_EachWithDifferentSubstances()
        {
            var d = TestData.Load();
            Assert.AreEqual(3, d.Stages.Count);
            var seen = new System.Collections.Generic.HashSet<string>();
            foreach (StageDef stage in d.Stages)
            {
                string key = "";
                foreach (var a in stage.Absorb) if (!d.IsDelivery(a.Key)) key += a.Key + ",";
                Assert.IsTrue(seen.Add(key), stage.Id + " 의 물질 구성이 다른 맵과 같다");
                Assert.GreaterOrEqual(stage.Absorb.Count, 8, stage.Id);
            }
        }

        [Test]
        public void EveryStage_CanTriggerSeveralReactions()
        {
            // 맵에서 얻을 수 있는 물질끼리만으로도 반응이 넷 이상 있어야 한다
            var d = TestData.Load();
            foreach (StageDef stage in d.Stages)
            {
                int count = 0;
                foreach (ReactionDef r in d.Reactions)
                    if (stage.AbsorbIndex(r.A) >= 0 && stage.AbsorbIndex(r.B) >= 0) count++;
                Assert.GreaterOrEqual(count, 4, stage.Id);
            }
        }

        [Test]
        public void UnknownBodyInStage_Throws()
        {
            string text = TestData.ReadCsv("stages").Replace("slime::2", "dragon::2");
            var ex = Assert.Throws<DataException>(() => TestData.LoadWith("stages", text));
            Assert.That(ex.Message, Does.Contain("dragon"));
        }

        [Test]
        public void MalformedStageEnemy_Throws()
        {
            string text = TestData.ReadCsv("stages").Replace("slime::2", "slime:2");
            var ex = Assert.Throws<DataException>(() => TestData.LoadWith("stages", text));
            Assert.That(ex.Message, Does.Contain("stages").And.Contain("enemies"));
        }

        [Test]
        public void EmptyRequiredNumber_Throws()
        {
            var ex = Assert.Throws<DataException>(() => TestData.LoadWith("enemies",
                "id,name,delivery,behavior,hp,speed,radius,contact_dps,color,shape\nhorn,가,bolt,chase,,1,0.3,1,#FFFFFF,square\n"));
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
            foreach (string key in new[] { "max_enemies", "max_zones", "max_projectiles", "max_reactions_per_tick", "chain_depth_limit", "upgrade_max" })
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

        [Test]
        public void EveryReaction_IsReachableOnSomeStage()
        {
            // 맵에 있는 물질 (적, 지형, 흡수) 에서 시작해 반응 결과물까지 따라가며, 일어날 수 있는 반응을 모은다
            var d = TestData.Load();
            var reachable = new System.Collections.Generic.HashSet<string>();
            foreach (StageDef stage in d.Stages)
            {
                var present = new System.Collections.Generic.HashSet<string>();
                foreach (StageEnemy e in stage.Enemies) if (e.Substance != null) present.Add(e.Substance);
                if (stage.TerrainSubstance != null) present.Add(stage.TerrainSubstance);
                foreach (var a in stage.Absorb) if (!d.IsDelivery(a.Key)) present.Add(a.Key);
                bool grew = true;
                while (grew)
                {
                    grew = false;
                    foreach (ReactionDef r in d.Reactions)
                    {
                        if (!present.Contains(r.A) || !present.Contains(r.B)) continue;
                        reachable.Add(r.Id);
                        foreach (EffectStep step in d.Effect(r.EffectId).Steps)
                            if (step.Substance != null && present.Add(step.Substance)) grew = true;
                    }
                }
            }
            foreach (ReactionDef r in d.Reactions)
                Assert.IsTrue(reachable.Contains(r.Id), r.Id + " (" + r.Name + ") 는 어느 맵에서도 일어날 수 없다");
        }
    }
}
