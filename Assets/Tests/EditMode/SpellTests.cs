using System.Collections.Generic;
using System.Numerics;
using Game.Core;
using Game.Data;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>마법 발사, 영역, 물질 묻히기 검사 (반응 제외)</summary>
    public class SpellTests
    {
        GameData data;
        Simulation sim;
        Vector2 player;

        [SetUp]
        public void SetUp()
        {
            data = TestData.Load();
            sim = SimHelpers.NewSim(data, quiet: true);
            player = sim.State.Player.Pos;
        }

        static Held Sub(GameData data, string id, int depth = 0)
        {
            return new Held { Id = id, TimeLeft = data.Substance(id).Duration, Depth = depth };
        }

        List<RunEvent> RunCollect(int ticks, RunEventType type)
        {
            var found = new List<RunEvent>();
            for (int i = 0; i < ticks; i++)
            {
                sim.State.Player.Hp = 100f;
                sim.Tick(TickInput.None);
                foreach (RunEvent e in sim.Events) if (e.Type == type) found.Add(e);
            }
            return found;
        }

        [Test]
        public void Bolt_HitsNearestEnemy_AndDealsDamage()
        {
            EnemyState far = SimHelpers.AddEnemy(sim, data, "horn", player + new Vector2(0f, 6f), 1000f);
            EnemyState near = SimHelpers.AddEnemy(sim, data, "horn", player + new Vector2(3f, 0f), 1000f);
            SimHelpers.Run(sim, 12);
            Assert.AreEqual(990f, near.Hp, 0.001f);
            Assert.AreEqual(1000f, far.Hp, 0.001f);
            Assert.AreEqual(0, sim.State.Projectiles.Count);
        }

        [Test]
        public void NoEnemyInRange_DoesNotFire()
        {
            SimHelpers.Run(sim, 60);
            Assert.AreEqual(0, sim.State.Projectiles.Count);
            Assert.LessOrEqual(sim.State.Slots[1].Cooldown, 0f);
        }

        [Test]
        public void Bolt_ThatMisses_DisappearsAfterRange()
        {
            EnemyState e = SimHelpers.AddEnemy(sim, data, "horn", player + new Vector2(8f, 0f), 1000f);
            sim.Tick(TickInput.None);
            Assert.AreEqual(1, sim.State.Projectiles.Count);
            sim.State.Enemies.Clear();
            SimHelpers.Run(sim, 40);
            Assert.AreEqual(0, sim.State.Projectiles.Count);
            Assert.AreEqual(1000f, e.Hp);
        }

        [Test]
        public void BoltWithSubstance_MarksEnemy()
        {
            sim.State.Slots[1].SubstanceId = "fire";
            EnemyState e = SimHelpers.AddEnemy(sim, data, "horn", player + new Vector2(3f, 0f), 1000f);
            SimHelpers.Run(sim, 12);
            Assert.AreEqual("fire", e.Substance.Id);
            Assert.AreEqual(0, e.Substance.Depth);
        }

        [Test]
        public void NonReactingSubstance_Overwrites()
        {
            EnemyState e = SimHelpers.AddEnemy(sim, data, "horn", player + new Vector2(15f, 0f), 1000f);
            Assert.IsFalse(sim.Resolver.ContactEnemy(e, Sub(data, "fire")));
            Assert.IsFalse(sim.Resolver.ContactEnemy(e, Sub(data, "lightning", 2)));
            Assert.AreEqual("lightning", e.Substance.Id);
            Assert.AreEqual(2, e.Substance.Depth);
            Assert.AreEqual(0.5f, e.Substance.TimeLeft, 0.001f);
        }

        [Test]
        public void EmptyIncoming_ChangesNothing()
        {
            EnemyState e = SimHelpers.AddEnemy(sim, data, "slime", player + new Vector2(15f, 0f), 1000f, "water");
            Assert.IsFalse(sim.Resolver.ContactEnemy(e, new Held()));
            Assert.AreEqual("water", e.Substance.Id);
        }

        [Test]
        public void SubstanceOnEnemy_ExpiresAfterDuration()
        {
            sim.State.Slots[1].DeliveryId = null;
            EnemyState e = SimHelpers.AddEnemy(sim, data, "horn", player + new Vector2(15f, 0f), 1000f);
            sim.Resolver.ContactEnemy(e, Sub(data, "fire"));
            SimHelpers.Run(sim, 85);
            Assert.AreEqual("fire", e.Substance.Id);
            SimHelpers.Run(sim, 7);
            Assert.IsTrue(e.Substance.IsEmpty);
        }

        [Test]
        public void InnateSubstance_DoesNotExpire()
        {
            sim.State.Slots[1].DeliveryId = null;
            EnemyState e = SimHelpers.AddEnemy(sim, data, "slime", player + new Vector2(15f, 0f), 1000f, "water");
            for (int i = 0; i < 600; i++) { sim.State.Player.Hp = 100f; sim.Tick(TickInput.None); }
            Assert.AreEqual("water", e.Substance.Id);
        }

        [Test]
        public void Cloud_SpawnsAtTarget()
        {
            sim.State.Slots[1].DeliveryId = "cloud";
            sim.State.Slots[1].SubstanceId = "gas";
            EnemyState e = SimHelpers.AddEnemy(sim, data, "horn", player + new Vector2(4f, 0f), 1000f);
            List<RunEvent> spawned = RunCollect(1, RunEventType.ZoneSpawned);
            Assert.AreEqual(1, spawned.Count);
            Assert.AreEqual(e.Pos.X, spawned[0].Pos.X, 0.001f);
            Assert.AreEqual("gas", sim.State.Zones[0].Substance.Id);
            Assert.AreEqual(2.2f, sim.State.Zones[0].Radius, 0.001f);
        }

        [Test]
        public void Zone_DamagesAndMarksEnemiesInside_OncePerInterval()
        {
            sim.State.Slots[1].DeliveryId = null;
            Vector2 at = player + new Vector2(15f, 0f);
            EnemyState inside = SimHelpers.AddEnemy(sim, data, "horn", at, 1000f);
            EnemyState outside = SimHelpers.AddEnemy(sim, data, "horn", at + new Vector2(0f, 6f), 1000f);
            sim.Resolver.SpawnZone(at, 3f, 10f, 3f, Sub(data, "gas"));
            SimHelpers.Run(sim, 10);
            Assert.AreEqual(997f, inside.Hp, 0.001f);
            Assert.AreEqual("gas", inside.Substance.Id);
            Assert.AreEqual(1000f, outside.Hp);
            Assert.IsTrue(outside.Substance.IsEmpty);
            SimHelpers.Run(sim, 12);
            Assert.AreEqual(994f, inside.Hp, 0.001f);
        }

        [Test]
        public void Zone_ExpiresAfterDuration_TerrainDoesNot()
        {
            sim.State.Slots[1].DeliveryId = null;
            sim.Resolver.SpawnZone(player + new Vector2(10f, 0f), 1f, 1f, 0f, Sub(data, "gas"));
            sim.Resolver.SpawnZone(player + new Vector2(-10f, 0f), 1f, -1f, 0f, new Held { Id = "water", TimeLeft = -1f });
            SimHelpers.Run(sim, 40);
            Assert.AreEqual(1, sim.State.Zones.Count);
            Assert.AreEqual("water", sim.State.Zones[0].Substance.Id);
        }

        [Test]
        public void ZoneCap_RemovesOldestNonTerrain()
        {
            Simulation s = SimHelpers.NewSim(data);
            s.State.SpawnTimer = 999999f;
            for (int i = 0; i < 50; i++)
                s.Resolver.SpawnZone(new Vector2(1f + i * 0.5f, 1f), 0.1f, 10f, 0f, Sub(data, "gas"));
            Assert.AreEqual(40, s.State.Zones.Count);
            int terrain = 0;
            foreach (ZoneState z in s.State.Zones) if (z.TimeLeft < 0f) terrain++;
            Assert.AreEqual(8, terrain);
        }

        [Test]
        public void ProjectileCap_IsRespected()
        {
            for (int i = 0; i < 100; i++)
                sim.State.Projectiles.Add(new ProjectileState { Id = sim.State.NextId++, Pos = new Vector2(1f, 1f), RangeLeft = 1000f, Radius = 0.1f });
            SimHelpers.AddEnemy(sim, data, "horn", player + new Vector2(3f, 0f), 1000f);
            SimHelpers.Run(sim, 5);
            Assert.AreEqual(100, sim.State.Projectiles.Count);
        }

        [Test]
        public void EmptySlot_NeverFires()
        {
            sim.State.Slots[1].DeliveryId = null;
            sim.State.Slots[0].SubstanceId = "fire";
            SimHelpers.AddEnemy(sim, data, "horn", player + new Vector2(3f, 0f), 1000f);
            SimHelpers.Run(sim, 30);
            Assert.AreEqual(0, sim.State.Projectiles.Count);
            Assert.AreEqual(0, sim.State.Zones.Count);
        }
    }
}
