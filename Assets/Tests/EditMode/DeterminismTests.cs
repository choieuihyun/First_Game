using System;
using System.IO;
using System.Numerics;
using Game.Core;
using Game.Data;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>같은 시드와 같은 입력이면 같은 판이 나오는지, 저장했다 불러도 그대로 이어지는지 검사</summary>
    public class DeterminismTests
    {
        GameData data;

        [SetUp]
        public void SetUp() { data = TestData.Load(); }

        /// <summary>틱 번호만으로 정해지는 입력. 원을 그리며 움직이고 룬은 돌아가며 끼운다</summary>
        static TickInput Script(int tick)
        {
            return new TickInput
            {
                Move = new Vector2((float)Math.Cos(tick * 0.05), (float)Math.Sin(tick * 0.05)),
                RuneChoice = tick % 3
            };
        }

        static void Run(Simulation sim, int from, int count)
        {
            for (int i = from; i < from + count; i++)
            {
                sim.State.Player.Hp = 100f;
                sim.Tick(Script(i));
            }
        }

        Simulation NewSim(uint seed)
        {
            var sim = new Simulation(data, RunFactory.NewRun(data, "swamp", seed));
            sim.State.Slots[0].SubstanceId = "fire";
            sim.State.Slots[1].DeliveryId = "cloud";
            sim.State.Slots[1].SubstanceId = "gas";
            sim.State.Slots[2].DeliveryId = "bolt";
            sim.State.Slots[2].SubstanceId = "lightning";
            return sim;
        }

        [Test]
        public void SameSeedSameInput_SameBytes()
        {
            Simulation a = NewSim(123), b = NewSim(123);
            Run(a, 0, 3000);
            Run(b, 0, 3000);
            Assert.Greater(a.State.Kills, 0);
            Assert.AreEqual(RunSerializer.Save(a.State), RunSerializer.Save(b.State));
        }

        [Test]
        public void DifferentSeed_DifferentBytes()
        {
            Simulation a = NewSim(123), b = NewSim(124);
            Run(a, 0, 600);
            Run(b, 0, 600);
            Assert.AreNotEqual(RunSerializer.Save(a.State), RunSerializer.Save(b.State));
        }

        [Test]
        public void SaveLoad_RoundTrip_IsIdentical()
        {
            Simulation a = NewSim(123);
            Run(a, 0, 1500);
            byte[] saved = RunSerializer.Save(a.State);
            RunState loaded = RunSerializer.Load(saved);
            Assert.AreEqual(saved, RunSerializer.Save(loaded));
            Assert.AreEqual(a.State.Tick, loaded.Tick);
            Assert.AreEqual(a.State.Enemies.Count, loaded.Enemies.Count);
            Assert.AreEqual(a.State.Slots[1].SubstanceId, loaded.Slots[1].SubstanceId);
        }

        [Test]
        public void LoadedRun_ContinuesIdentically()
        {
            Simulation a = NewSim(77);
            Run(a, 0, 1500);
            var b = new Simulation(data, RunSerializer.Load(RunSerializer.Save(a.State)));
            Run(a, 1500, 1500);
            Run(b, 1500, 1500);
            Assert.AreEqual(RunSerializer.Save(a.State), RunSerializer.Save(b.State));
        }

        [Test]
        public void SaveWhileChoosingRune_Restores()
        {
            Simulation a = NewSim(5);
            a.State.Pickups.Add(new PickupState { Id = a.State.NextId++, Pos = a.State.Player.Pos, RuneId = "water" });
            a.Tick(TickInput.None);
            Assert.AreEqual(RunPhase.ChoosingRune, a.State.Phase);
            RunState loaded = RunSerializer.Load(RunSerializer.Save(a.State));
            Assert.AreEqual(RunPhase.ChoosingRune, loaded.Phase);
            Assert.AreEqual(a.State.PendingRunes, loaded.PendingRunes);
        }

        [Test]
        public void EmptySubstanceAndNullStrings_RoundTrip()
        {
            RunState fresh = RunFactory.NewRun(data, "swamp", 9);
            RunState loaded = RunSerializer.Load(RunSerializer.Save(fresh));
            Assert.IsNull(loaded.Slots[1].DeliveryId);
            Assert.IsNull(loaded.Slots[0].SubstanceId);
            Assert.AreEqual("bolt", loaded.Slots[0].DeliveryId);
            Assert.AreEqual(fresh.Rng.State, loaded.Rng.State);
            Assert.AreEqual("swamp", loaded.StageId);
        }

        [Test]
        public void WrongVersion_Throws()
        {
            byte[] bytes = RunSerializer.Save(RunFactory.NewRun(data, "swamp", 1));
            bytes[0] = 99;
            Assert.Throws<InvalidDataException>(() => RunSerializer.Load(bytes));
        }

        [Test]
        public void TruncatedBytes_Throws()
        {
            Simulation a = NewSim(123);
            Run(a, 0, 600);
            byte[] bytes = RunSerializer.Save(a.State);
            var half = new byte[bytes.Length / 2];
            Array.Copy(bytes, half, half.Length);
            Assert.Throws<InvalidDataException>(() => RunSerializer.Load(half));
            Assert.Throws<InvalidDataException>(() => RunSerializer.Load(new byte[0]));
            Assert.Throws<InvalidDataException>(() => RunSerializer.Load(null));
        }

        [Test]
        public void TwentyMinutes_NoExceptions_CapsHold_ReactionsHappen()
        {
            for (uint seed = 1; seed <= 5; seed++)
            {
                Simulation sim = NewSim(seed);
                for (int i = 0; i < 36000; i++)
                {
                    sim.State.Player.Hp = 100f;
                    sim.Tick(Script(i));
                    Assert.LessOrEqual(sim.State.Enemies.Count, 150);
                    Assert.LessOrEqual(sim.State.Zones.Count, 40);
                    Assert.LessOrEqual(sim.State.Projectiles.Count, 100);
                    Assert.LessOrEqual(sim.State.ReactionQueue.Count, 1000);
                }
                Assert.Greater(sim.State.ReactionCount, 0, "시드 " + seed);
                Assert.Greater(sim.State.Kills, 0, "시드 " + seed);
                TestContext.WriteLine("시드 " + seed + ": 처치 " + sim.State.Kills + ", 반응 " + sim.State.ReactionCount);
            }
        }
    }
}
