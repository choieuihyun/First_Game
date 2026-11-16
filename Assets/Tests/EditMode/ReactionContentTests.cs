using System.Collections.Generic;
using System.Numerics;
using Game.Core;
using Game.Data;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>늘어난 반응과 새 효과 검사. 얼리기, 느려짐, 지속 피해, 약화, 밀고 당기기, 물질이 바뀌는 반응</summary>
    public class ReactionContentTests
    {
        GameData data;
        Simulation sim;
        RunState state;
        Vector2 far;

        [SetUp]
        public void SetUp()
        {
            data = TestData.Load();
            sim = SimHelpers.NewSim(data, quiet: true);
            state = sim.State;
            foreach (SpellSlot slot in state.Slots) slot.DeliveryId = null;
            far = state.Player.Pos + new Vector2(12f, 0f);
        }

        Held Sub(string id) { return new Held { Id = id, TimeLeft = data.Substance(id).Duration, Depth = 0 }; }

        EnemyState Enemy(Vector2 pos, string substance = null)
        {
            return SimHelpers.AddEnemy(sim, data, "slime", pos, 1000f, substance);
        }

        void Tick(int ticks = 1)
        {
            for (int i = 0; i < ticks; i++) { state.Player.Hp = 100f; sim.Tick(TickInput.None); }
        }

        [Test]
        public void Data_HasWideReactionTable()
        {
            Assert.GreaterOrEqual(data.Reactions.Count, 20);
            // 흡수할 수 있는 물질마다 반응이 셋 이상 있어야 조합할 거리가 된다
            foreach (SubstanceDef s in data.Substances)
            {
                if (!s.IsRune) continue;
                int count = 0;
                foreach (ReactionDef r in data.Reactions) if (r.A == s.Id || r.B == s.Id) count++;
                Assert.GreaterOrEqual(count, 3, s.Id);
            }
        }

        [Test]
        public void Data_ReactionsUseManyKindsOfOutcome()
        {
            var pieces = new HashSet<EffectPiece>();
            var statuses = new HashSet<string>();
            foreach (ReactionDef r in data.Reactions)
                foreach (EffectStep step in data.Effect(r.EffectId).Steps)
                {
                    pieces.Add(step.Piece);
                    if (step.Status != null) statuses.Add(step.Status);
                }
            Assert.That(pieces, Is.SupersetOf(new[] { EffectPiece.Damage, EffectPiece.SpawnZone, EffectPiece.Explode, EffectPiece.Chain, EffectPiece.Status, EffectPiece.Push }));
            Assert.That(statuses, Is.SupersetOf(new[] { "slow", "freeze", "dot", "vulnerable" }));
        }

        [Test]
        public void WaterPlusIce_FreezesEnemiesInPlace()
        {
            EnemyState wet = Enemy(far, "water");
            EnemyState near = Enemy(far + new Vector2(1f, 0f));
            EnemyState away = Enemy(far + new Vector2(0f, 6f));
            Assert.IsTrue(sim.Resolver.ContactEnemy(wet, Sub("ice")));
            Tick();
            Vector2 wetPos = wet.Pos, nearPos = near.Pos, awayPos = away.Pos;
            Tick(30);
            Assert.AreEqual(wetPos, wet.Pos);
            Assert.AreEqual(nearPos, near.Pos);
            Assert.AreNotEqual(awayPos, away.Pos);
            Tick(60);
            Assert.AreNotEqual(wetPos, wet.Pos);
        }

        [Test]
        public void FrozenEnemy_DoesNotHurtPlayer()
        {
            EnemyState e = Enemy(state.Player.Pos);
            e.FreezeTime = 5f;
            SimHelpers.Run(sim, 20);
            Assert.AreEqual(100f, state.Player.Hp);
        }

        [Test]
        public void OilPlusWater_SlowsEnemies()
        {
            EnemyState oily = Enemy(far, "oil");
            EnemyState normal = Enemy(far + new Vector2(0f, 8f));
            sim.Resolver.ContactEnemy(oily, Sub("water"));
            Tick();
            Vector2 a = oily.Pos, b = normal.Pos;
            Tick(30);
            float slowMoved = Vector2.Distance(a, oily.Pos);
            float normalMoved = Vector2.Distance(b, normal.Pos);
            Assert.AreEqual(normalMoved * 0.4f, slowMoved, 0.02f);
        }

        [Test]
        public void AcidPlusGas_PoisonsOverTime_AndKillCounts()
        {
            EnemyState e = Enemy(far, "acid");
            sim.Resolver.ContactEnemy(e, Sub("gas"));
            Tick(31);
            Assert.AreEqual(1000f - 6f, e.Hp, 0.5f);
            e.Hp = 1f;
            Tick(10);
            Assert.IsFalse(state.Enemies.Contains(e));
            Assert.AreEqual(1, state.Kills);
        }

        [Test]
        public void AcidPlusMetal_MakesEnemiesTakeMoreDamage_AndLeavesHydrogen()
        {
            EnemyState e = Enemy(far, "metal");
            sim.Resolver.ContactEnemy(e, Sub("acid"));
            Tick();
            Assert.Greater(e.VulnTime, 0f);
            float before = e.Hp;
            Combat.Damage(e, 10f);
            Assert.AreEqual(before - 15f, e.Hp, 0.001f);
            Assert.IsTrue(state.Zones.Exists(z => z.Substance.Id == "hydrogen"));
        }

        [Test]
        public void WaterPlusFire_PushesEnemiesAway_AndLeavesSteam()
        {
            EnemyState wet = Enemy(far, "water");
            EnemyState bystander = Enemy(far + new Vector2(1.5f, 0f));
            float before = Vector2.Distance(bystander.Pos, far);
            sim.Resolver.ContactEnemy(wet, Sub("fire"));
            Tick();
            Assert.Greater(Vector2.Distance(bystander.Pos, far), before + 1.5f);
            Assert.IsTrue(state.Zones.Exists(z => z.Substance.Id == "steam"));
        }

        [Test]
        public void MetalPlusLightning_PullsEnemiesTogether_WithoutOvershooting()
        {
            EnemyState metal = Enemy(far, "metal");
            EnemyState a = Enemy(far + new Vector2(3f, 0f));
            EnemyState b = Enemy(far + new Vector2(0.5f, 0f));
            sim.Resolver.ContactEnemy(metal, Sub("lightning"));
            Tick();
            Assert.Less(Vector2.Distance(a.Pos, far), 1.6f);
            // 끌려와도 중심을 지나쳐 반대편으로 넘어가지는 않는다
            Assert.Greater(b.Pos.X, far.X - 0.2f);
        }

        [Test]
        public void Push_KeepsEnemiesInsideMap()
        {
            EnemyState wet = Enemy(new Vector2(1.1f, 1.1f), "water");
            EnemyState cornered = Enemy(new Vector2(0.2f, 0.2f));
            sim.Resolver.ContactEnemy(wet, Sub("fire"));
            Tick();
            Assert.GreaterOrEqual(cornered.Pos.X, 0f);
            Assert.GreaterOrEqual(cornered.Pos.Y, 0f);
        }

        [Test]
        public void OilPlusFire_LeavesBurningGround_ThatIgnitesGas()
        {
            EnemyState oily = Enemy(far, "oil");
            sim.Resolver.ContactEnemy(oily, Sub("fire"));
            Tick();
            ZoneState burning = state.Zones.Find(z => z.Substance.Id == "fire");
            Assert.IsNotNull(burning);
            Assert.AreEqual(6f, burning.Damage, 0.001f);
            int before = state.ReactionCount;
            sim.Resolver.SpawnZone(far + new Vector2(1f, 0f), 1.5f, 10f, 0f, new Held { Id = "gas", TimeLeft = -1f });
            Tick(20);
            Assert.Greater(state.ReactionCount, before);
        }

        [Test]
        public void IcePlusFire_MeltsIntoWater_AndHurtsTarget()
        {
            EnemyState icy = Enemy(far, "ice");
            sim.Resolver.ContactEnemy(icy, Sub("fire"));
            Tick();
            Assert.AreEqual(988f, icy.Hp, 0.001f);
            Assert.IsTrue(state.Zones.Exists(z => z.Substance.Id == "water"));
        }

        [Test]
        public void GasPlusWater_BecomesAcid_WhichCanCorrodeMetal()
        {
            // 물질이 다른 물질로 바뀌어 다음 반응의 재료가 된다
            EnemyState gassy = Enemy(far, "gas");
            sim.Resolver.ContactEnemy(gassy, Sub("water"));
            Tick();
            ZoneState acid = state.Zones.Find(z => z.Substance.Id == "acid");
            Assert.IsNotNull(acid);
            EnemyState metal = Enemy(acid.Pos, "metal");
            Tick(20);
            Assert.Greater(metal.VulnTime, 0f);
        }

        [Test]
        public void StatusOnTargetOnly_WhenRadiusIsZero()
        {
            EnemyState metal = Enemy(far, "metal");
            EnemyState neighbour = Enemy(far + new Vector2(0.8f, 0f));
            sim.Resolver.ContactEnemy(metal, Sub("ice"));
            Tick();
            Assert.Greater(metal.VulnTime, 0f);
            Assert.AreEqual(0f, neighbour.VulnTime);
            Assert.Less(metal.Hp, 1000f);
        }

        [Test]
        public void EveryReaction_RunsWithoutError()
        {
            foreach (ReactionDef r in data.Reactions)
            {
                Simulation s = SimHelpers.NewSim(data, quiet: true);
                foreach (SpellSlot slot in s.State.Slots) slot.DeliveryId = null;
                Vector2 at = s.State.Player.Pos + new Vector2(10f, 0f);
                EnemyState target = SimHelpers.AddEnemy(s, data, "slime", at, 1000f);
                target.Substance = new Held { Id = r.A, TimeLeft = 5f };
                for (int i = 0; i < 6; i++) SimHelpers.AddEnemy(s, data, "horn", at + new Vector2(0.5f + i * 0.4f, 0.3f), 1000f);
                Assert.IsTrue(s.Resolver.ContactEnemy(target, new Held { Id = r.B, TimeLeft = 5f }), r.Id);
                for (int i = 0; i < 60; i++) { s.State.Player.Hp = 100f; s.Tick(TickInput.None); }
                Assert.GreaterOrEqual(s.State.ReactionCount, 1, r.Id);
            }
        }
    }
}
