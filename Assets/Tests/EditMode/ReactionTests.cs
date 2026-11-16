using System.Collections.Generic;
using System.Numerics;
using Game.Core;
using Game.Data;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>전장 반응 검사. 가스 폭발, 전기분해, 수소 대폭발, 연쇄 한도, 틱당 상한</summary>
    public class ReactionTests
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
            state.Slots[1].DeliveryId = null;
            // 플레이어에게서 멀리 떨어진 시험 장소
            far = state.Player.Pos + new Vector2(12f, 0f);
        }

        Held Sub(string id, int depth = 0)
        {
            return new Held { Id = id, TimeLeft = data.Substance(id).Duration, Depth = depth };
        }

        EnemyState Enemy(string kind, Vector2 pos)
        {
            // soaked 는 물을 지닌 점액형, 그 밖에는 물질 없는 뿔형
            return kind == "soaked"
                ? SimHelpers.AddEnemy(sim, data, "slime", pos, 1000f, "water")
                : SimHelpers.AddEnemy(sim, data, "horn", pos, 1000f);
        }

        ZoneState Zone(string substance, Vector2 pos, float radius = 1.8f)
        {
            return sim.Resolver.SpawnZone(pos, radius, 30f, 0f, new Held { Id = substance, TimeLeft = -1f });
        }

        List<RunEvent> TickCollect(int ticks = 1)
        {
            var all = new List<RunEvent>();
            for (int i = 0; i < ticks; i++)
            {
                state.Player.Hp = 100f;
                sim.Tick(TickInput.None);
                all.AddRange(sim.Events);
            }
            return all;
        }

        [Test]
        public void Fire_OnGasCloud_Explodes_AndCloudVanishes()
        {
            ZoneState gas = Zone("gas", far);
            EnemyState near = Enemy("horn", far + new Vector2(2f, 0f));
            EnemyState away = Enemy("horn", far + new Vector2(0f, 5f));
            Assert.IsTrue(sim.Resolver.ContactZone(gas, Sub("fire"), far));
            List<RunEvent> events = TickCollect();
            Assert.IsFalse(state.Zones.Contains(gas));
            Assert.AreEqual(960f, near.Hp, 0.001f);
            Assert.AreEqual(1000f, away.Hp);
            Assert.AreEqual(1, state.ReactionCount);
            Assert.IsTrue(events.Exists(e => e.Type == RunEventType.Reaction && e.Id == "gas_explosion"));
            Assert.IsTrue(events.Exists(e => e.Type == RunEventType.Explosion && e.Radius == 2.5f));
        }

        [Test]
        public void FireBolt_FliesIntoGasCloud_AndExplodes()
        {
            state.Slots[1].DeliveryId = "bolt";
            state.Slots[1].SubstanceId = "fire";
            Zone("gas", state.Player.Pos + new Vector2(3f, 0f), 1f);
            Enemy("horn", state.Player.Pos + new Vector2(8f, 0f));
            TickCollect(15);
            Assert.AreEqual(1, state.ReactionCount);
            Assert.AreEqual(0, state.Zones.Count);
            Assert.AreEqual(100f, state.Player.Hp);
        }

        [Test]
        public void Lightning_OnSoakedEnemy_ChainsAndLeavesHydrogen()
        {
            EnemyState soaked = Enemy("soaked", far);
            EnemyState a = Enemy("horn", far + new Vector2(1.5f, 0f));
            EnemyState b = Enemy("horn", far + new Vector2(3f, 0f));
            EnemyState c = Enemy("horn", far + new Vector2(4.5f, 0f));
            EnemyState tooFar = Enemy("horn", far + new Vector2(0f, -8f));
            Assert.IsTrue(sim.Resolver.ContactEnemy(soaked, Sub("lightning")));
            Assert.IsTrue(soaked.Substance.IsEmpty);
            List<RunEvent> events = TickCollect();
            Assert.AreEqual(985f, a.Hp, 0.001f);
            Assert.AreEqual(985f, b.Hp, 0.001f);
            Assert.AreEqual(985f, c.Hp, 0.001f);
            Assert.AreEqual(1000f, soaked.Hp);
            Assert.AreEqual(1000f, tooFar.Hp);
            Assert.AreEqual(3, events.FindAll(e => e.Type == RunEventType.ChainLink).Count);
            Assert.AreEqual(1, state.Zones.Count);
            ZoneState h = state.Zones[0];
            Assert.AreEqual("hydrogen", h.Substance.Id);
            Assert.AreEqual(1.5f, h.Radius, 0.001f);
            Assert.AreEqual(4f, h.TimeLeft, 0.1f);
            Assert.AreEqual(1, h.Substance.Depth);
        }

        [Test]
        public void ThreeStepChain_WaterLightningFire()
        {
            EnemyState soaked = Enemy("soaked", far);
            EnemyState bystander = Enemy("horn", far + new Vector2(3.5f, 0f));
            sim.Resolver.ContactEnemy(soaked, Sub("lightning"));
            TickCollect();
            ZoneState hydrogen = state.Zones[0];
            float before = bystander.Hp;
            Assert.IsTrue(sim.Resolver.ContactZone(hydrogen, Sub("fire"), hydrogen.Pos));
            List<RunEvent> events = TickCollect();
            Assert.AreEqual(before - 80f, bystander.Hp, 0.001f);
            Assert.AreEqual(2, state.ReactionCount);
            Assert.IsTrue(events.Exists(e => e.Type == RunEventType.Reaction && e.Id == "hydrogen_blast"));
        }

        [Test]
        public void GasExplosion_IgnitesNeighbourGasCloud()
        {
            ZoneState first = Zone("gas", far);
            Zone("gas", far + new Vector2(3f, 0f));
            sim.Resolver.ContactZone(first, Sub("fire"), far);
            TickCollect(2);
            Assert.AreEqual(0, state.Zones.Count);
            Assert.AreEqual(2, state.ReactionCount);
        }

        [Test]
        public void BurningEnemy_InsideGasCloud_Explodes()
        {
            ZoneState gas = Zone("gas", far);
            EnemyState e = Enemy("horn", far);
            e.Substance = Sub("fire");
            TickCollect(2);
            Assert.IsFalse(state.Zones.Contains(gas));
            Assert.AreEqual(1, state.ReactionCount);
            Assert.AreEqual(960f, e.Hp, 0.001f);
            // 폭발의 불이 다시 옮겨 붙는다
            Assert.AreEqual("fire", e.Substance.Id);
            Assert.AreEqual(1, e.Substance.Depth);
        }

        [Test]
        public void DepthLimit_StopsChain()
        {
            ZoneState gas = Zone("gas", far);
            Assert.IsFalse(sim.Resolver.ContactZone(gas, Sub("fire", 4), far));
            EnemyState e = Enemy("horn", far + new Vector2(6f, 0f));
            e.Substance = new Held { Id = "gas", TimeLeft = 4f, Depth = 4 };
            Assert.IsFalse(sim.Resolver.ContactEnemy(e, Sub("fire")));
            Assert.AreEqual("fire", e.Substance.Id);
            TickCollect();
            Assert.IsTrue(state.Zones.Contains(gas));
            Assert.AreEqual(0, state.ReactionCount);
        }

        [Test]
        public void DepthThree_StillReacts()
        {
            ZoneState gas = Zone("gas", far);
            Assert.IsTrue(sim.Resolver.ContactZone(gas, Sub("fire", 3), far));
        }

        [Test]
        public void ManyClouds_RespectPerTickCap_AndDrain()
        {
            var zones = new List<ZoneState>();
            for (int i = 0; i < 30; i++) zones.Add(Zone("gas", far + new Vector2(i * 0.05f, 0f)));
            sim.Resolver.ContactZone(zones[0], Sub("fire"), far);
            int ticks = 0;
            int previous = 0;
            do
            {
                TickCollect();
                ticks++;
                Assert.LessOrEqual(state.ReactionCount - previous, 20);
                previous = state.ReactionCount;
            } while (state.ReactionQueue.Count > 0 && ticks < 10);
            Assert.AreEqual(0, state.ReactionQueue.Count);
            Assert.AreEqual(30, state.ReactionCount);
            Assert.AreEqual(2, ticks);
            Assert.AreEqual(0, state.Zones.Count);
        }

        [Test]
        public void Explosion_DoesNotHurtPlayer()
        {
            ZoneState gas = Zone("gas", state.Player.Pos);
            sim.Resolver.ContactZone(gas, Sub("fire"), state.Player.Pos);
            sim.Tick(TickInput.None);
            Assert.AreEqual(1, state.ReactionCount);
            Assert.AreEqual(100f, state.Player.Hp);
        }

        [Test]
        public void DeadTarget_DoesNotBreakQueuedReaction()
        {
            EnemyState soaked = Enemy("soaked", far);
            sim.Resolver.ContactEnemy(soaked, Sub("lightning"));
            state.Enemies.Remove(soaked);
            sim.Grid.Rebuild(state.Enemies);
            Assert.DoesNotThrow(() => sim.Tick(TickInput.None));
            Assert.AreEqual(1, state.ReactionCount);
        }

        [Test]
        public void ZoneReactingWithEnemy_ConsumesBoth()
        {
            ZoneState hydrogen = Zone("hydrogen", far, 1.5f);
            EnemyState burning = Enemy("horn", far);
            burning.Substance = Sub("fire");
            TickCollect(2);
            Assert.IsFalse(state.Zones.Contains(hydrogen));
            Assert.AreEqual(920f, burning.Hp, 0.001f);
        }

        [Test]
        public void ExplosionKill_CountsAsKill()
        {
            ZoneState gas = Zone("gas", far);
            EnemyState weak = SimHelpers.AddEnemy(sim, data, "horn", far + new Vector2(2f, 0f), 30f);
            sim.Grid.Rebuild(state.Enemies);
            sim.Resolver.ContactZone(gas, Sub("fire"), far);
            sim.Tick(TickInput.None);
            Assert.IsFalse(state.Enemies.Contains(weak));
            Assert.AreEqual(1, state.Kills);
        }
    }
}
