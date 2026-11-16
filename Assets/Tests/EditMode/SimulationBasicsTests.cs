using System.Numerics;
using Game.Core;
using Game.Data;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>이동, 적 생성, 접촉 피해, 사망 검사</summary>
    public class SimulationBasicsTests
    {
        GameData data;

        [SetUp]
        public void SetUp() { data = TestData.Load(); }

        [Test]
        public void NewRun_StartsCenteredWithBoltAndTerrain()
        {
            RunState s = RunFactory.NewRun(data, "swamp", 1);
            Assert.AreEqual(new Vector2(20f, 20f), s.Player.Pos);
            Assert.AreEqual(100f, s.Player.Hp);
            Assert.AreEqual(RunPhase.Playing, s.Phase);
            Assert.AreEqual(3, s.Slots.Length);
            Assert.AreEqual("bolt", s.Slots[BodyPart.RightArm].DeliveryId);
            Assert.AreEqual(1, s.Slots[BodyPart.RightArm].DeliveryLevel);
            Assert.IsNull(s.Slots[BodyPart.LeftArm].DeliveryId);
            Assert.IsNull(s.Slots[BodyPart.Tail].DeliveryId);
            Assert.AreEqual(new Vector2(0f, 1f), s.Player.Facing);
            Assert.AreEqual(7, s.AbsorbCount.Length);
            Assert.AreEqual(8, s.Zones.Count);
            foreach (ZoneState z in s.Zones)
            {
                Assert.AreEqual("water", z.Substance.Id);
                Assert.Less(z.TimeLeft, 0f);
                Assert.That(z.Pos.X, Is.InRange(0f, 40f));
                Assert.That(z.Pos.Y, Is.InRange(0f, 40f));
                Assert.GreaterOrEqual(Vector2.Distance(z.Pos, s.Player.Pos), 3f);
            }
        }

        [Test]
        public void Player_MovesAtSpeed_AndStopsAtMapEdge()
        {
            Simulation sim = SimHelpers.NewSim(data, quiet: true);
            SimHelpers.Run(sim, 30, SimHelpers.Move(1f, 0f));
            Assert.AreEqual(25f, sim.State.Player.Pos.X, 0.01f);
            SimHelpers.Run(sim, 600, SimHelpers.Move(1f, 0f));
            Assert.LessOrEqual(sim.State.Player.Pos.X, 40f);
            Assert.Greater(sim.State.Player.Pos.X, 39f);
        }

        [Test]
        public void Move_LongerThanOne_IsClamped()
        {
            Simulation sim = SimHelpers.NewSim(data, quiet: true);
            SimHelpers.Run(sim, 30, SimHelpers.Move(10f, 0f));
            Assert.AreEqual(25f, sim.State.Player.Pos.X, 0.01f);
        }

        [Test]
        public void Tick_AdvancesTickCounter()
        {
            Simulation sim = SimHelpers.NewSim(data, quiet: true);
            SimHelpers.Run(sim, 45);
            Assert.AreEqual(45, sim.State.Tick);
            Assert.AreEqual(1.5f, sim.State.Seconds(data), 0.0001f);
        }

        [Test]
        public void Enemies_SpawnInsideMap_AndApproach()
        {
            Simulation sim = SimHelpers.NewSim(data);
            sim.State.Slots[1].DeliveryId = null;
            for (int i = 0; i < 150; i++) { sim.State.Player.Hp = 100f; sim.Tick(TickInput.None); }
            Assert.Greater(sim.State.Enemies.Count, 0);
            EnemyState first = sim.State.Enemies[0];
            float before = Vector2.Distance(first.Pos, sim.State.Player.Pos);
            foreach (EnemyState e in sim.State.Enemies)
            {
                Assert.That(e.Pos.X, Is.InRange(0f, 40f));
                Assert.That(e.Pos.Y, Is.InRange(0f, 40f));
            }
            sim.Tick(TickInput.None);
            Assert.LessOrEqual(Vector2.Distance(first.Pos, sim.State.Player.Pos), before);
        }

        [Test]
        public void EnemyCount_NeverExceedsCap()
        {
            Simulation sim = SimHelpers.NewSim(data);
            sim.State.Slots[1].DeliveryId = null;
            int max = 0;
            for (int i = 0; i < 36000; i++)
            {
                sim.State.Player.Hp = 100f;
                sim.Tick(TickInput.None);
                if (sim.State.Enemies.Count > max) max = sim.State.Enemies.Count;
            }
            Assert.LessOrEqual(max, 150);
            Assert.AreEqual(150, max);
        }

        [Test]
        public void IdlePlayer_Dies_AndSimulationStops()
        {
            Simulation sim = SimHelpers.NewSim(data);
            sim.State.Slots[1].DeliveryId = null;
            int deathEvents = 0;
            for (int i = 0; i < 3600 && sim.State.Phase != RunPhase.Dead; i++)
            {
                sim.Tick(TickInput.None);
                foreach (RunEvent e in sim.Events) if (e.Type == RunEventType.PlayerDied) deathEvents++;
            }
            Assert.AreEqual(RunPhase.Dead, sim.State.Phase);
            Assert.AreEqual(1, deathEvents);
            int tick = sim.State.Tick;
            SimHelpers.Run(sim, 10);
            Assert.AreEqual(tick, sim.State.Tick);
        }

        [Test]
        public void DeadEnemy_IsRemoved_AndCounted()
        {
            Simulation sim = SimHelpers.NewSim(data, quiet: true);
            EnemyState e = SimHelpers.AddEnemy(sim, data, "horn", new Vector2(30f, 30f));
            e.Hp = 0f;
            sim.Tick(TickInput.None);
            Assert.AreEqual(0, sim.State.Enemies.Count);
            Assert.AreEqual(1, sim.State.Kills);
            Assert.IsTrue(sim.Events.Exists(ev => ev.Type == RunEventType.EnemyDied));
        }

        [Test]
        public void TouchingEnemy_DamagesPlayer()
        {
            Simulation sim = SimHelpers.NewSim(data, quiet: true);
            sim.State.Slots[1].DeliveryId = null;
            SimHelpers.AddEnemy(sim, data, "horn", sim.State.Player.Pos);
            SimHelpers.Run(sim, 30);
            Assert.AreEqual(88f, sim.State.Player.Hp, 0.1f);
        }

        [Test]
        public void LateEnemies_HaveMoreHp()
        {
            Simulation sim = SimHelpers.NewSim(data);
            sim.State.Slots[1].DeliveryId = null;
            sim.State.Tick = 30 * 60 * 10;
            sim.State.SpawnTimer = 0f;
            sim.State.Player.Hp = 100f;
            sim.Tick(TickInput.None);
            Assert.Greater(sim.State.Enemies.Count, 0);
            EnemyState e = sim.State.Enemies[0];
            Assert.AreEqual(data.Enemy(e.DefId).Hp * 9f, e.Hp, 0.5f);
        }

        [Test]
        public void EnemiesNeverSpawnOnTopOfPlayer_InCorner()
        {
            // 모서리에 몰려도 적이 플레이어 바로 위에 생기면 안 된다
            Simulation sim = SimHelpers.NewSim(data);
            sim.State.Slots[1].DeliveryId = null;
            float minAllowed = data.Balance("spawn_distance") * 0.5f;
            var seen = new System.Collections.Generic.HashSet<int>();
            int spawned = 0;
            for (int i = 0; i < 1800; i++)
            {
                sim.State.Player.Hp = 100f;
                sim.State.Player.Pos = new Vector2(0f, 0f);
                sim.State.Tick = 30 * 60 * 8;
                sim.Tick(TickInput.None);
                foreach (EnemyState e in sim.State.Enemies)
                {
                    if (!seen.Add(e.Id)) continue;
                    spawned++;
                    // 생긴 틱에 한 번 움직였으므로 그만큼은 봐 준다
                    Assert.GreaterOrEqual(Vector2.Distance(e.Pos, sim.State.Player.Pos), minAllowed - 0.2f, "적 " + e.Id);
                }
                sim.State.Enemies.Clear();
            }
            Assert.Greater(spawned, 50);
        }

        [Test]
        public void NewRun_UsesStartDeliveryFromStage()
        {
            string stages = TestData.ReadCsv("stages").TrimEnd();
            GameData changed = TestData.LoadWith("stages", stages.Substring(0, stages.Length - 4) + "pool\n");
            Assert.AreEqual("pool", changed.Stage("swamp").StartDelivery);
            Assert.AreEqual("pool", RunFactory.NewRun(changed, "swamp", 1).Slots[1].DeliveryId);
        }
    }
}
