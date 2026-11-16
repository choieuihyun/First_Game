using System.Collections.Generic;
using System.Numerics;
using Game.Core;
using Game.Data;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>룬 드롭, 줍기, 멈추고 고르기 검사</summary>
    public class RuneTests
    {
        GameData data;
        Simulation sim;
        RunState state;

        [SetUp]
        public void SetUp()
        {
            data = TestData.Load();
            sim = SimHelpers.NewSim(data, quiet: true);
            state = sim.State;
        }

        void DropAtPlayer(string rune)
        {
            state.Pickups.Add(new PickupState { Id = state.NextId++, Pos = state.Player.Pos, RuneId = rune });
        }

        static TickInput Choose(int choice)
        {
            TickInput input = TickInput.None;
            input.RuneChoice = choice;
            return input;
        }

        [Test]
        public void WalkingOverRune_PausesRun()
        {
            DropAtPlayer("fire");
            sim.Tick(TickInput.None);
            Assert.AreEqual(RunPhase.ChoosingRune, state.Phase);
            Assert.AreEqual(new List<string> { "fire" }, state.PendingRunes);
            Assert.AreEqual(0, state.Pickups.Count);
            Assert.IsTrue(sim.Events.Exists(e => e.Type == RunEventType.RunePending && e.Id == "fire"));
        }

        [Test]
        public void FarRune_IsNotPickedUp()
        {
            state.Pickups.Add(new PickupState { Id = state.NextId++, Pos = state.Player.Pos + new Vector2(3f, 0f), RuneId = "fire" });
            SimHelpers.Run(sim, 10);
            Assert.AreEqual(RunPhase.Playing, state.Phase);
            Assert.AreEqual(1, state.Pickups.Count);
        }

        [Test]
        public void WhileChoosing_TimeDoesNotAdvance()
        {
            EnemyState e = SimHelpers.AddEnemy(sim, data, "grunt", state.Player.Pos + new Vector2(15f, 0f), 1000f);
            DropAtPlayer("fire");
            sim.Tick(TickInput.None);
            int tick = state.Tick;
            Vector2 pos = e.Pos;
            SimHelpers.Run(sim, 10);
            Assert.AreEqual(tick, state.Tick);
            Assert.AreEqual(pos, e.Pos);
            Assert.AreEqual(RunPhase.ChoosingRune, state.Phase);
        }

        [Test]
        public void ChoosingSlot_EquipsSubstance_AndResumes()
        {
            DropAtPlayer("fire");
            sim.Tick(TickInput.None);
            sim.Tick(Choose(1));
            Assert.AreEqual("fire", state.Slots[1].SubstanceId);
            Assert.IsNull(state.Slots[1].DeliveryId);
            Assert.AreEqual(RunPhase.Playing, state.Phase);
            Assert.AreEqual(0, state.PendingRunes.Count);
            Assert.IsTrue(sim.Events.Exists(e => e.Type == RunEventType.RuneEquipped && e.Id == "fire"));
        }

        [Test]
        public void DeliveryRune_ReplacesDelivery_KeepsSubstance()
        {
            state.Slots[0].SubstanceId = "fire";
            state.Slots[0].Cooldown = 0.4f;
            DropAtPlayer("cloud");
            sim.Tick(TickInput.None);
            sim.Tick(Choose(0));
            Assert.AreEqual("cloud", state.Slots[0].DeliveryId);
            Assert.AreEqual("fire", state.Slots[0].SubstanceId);
            Assert.AreEqual(0f, state.Slots[0].Cooldown);
        }

        [Test]
        public void Discard_DropsRune_AndResumes()
        {
            DropAtPlayer("fire");
            sim.Tick(TickInput.None);
            sim.Tick(Choose(TickInput.Discard));
            Assert.AreEqual(RunPhase.Playing, state.Phase);
            foreach (SpellSlot slot in state.Slots) Assert.IsNull(slot.SubstanceId);
        }

        [Test]
        public void TwoRunesSameTick_AreChosenOneByOne()
        {
            DropAtPlayer("fire");
            DropAtPlayer("cloud");
            sim.Tick(TickInput.None);
            Assert.AreEqual(2, state.PendingRunes.Count);
            sim.Tick(Choose(1));
            Assert.AreEqual(RunPhase.ChoosingRune, state.Phase);
            Assert.AreEqual(new List<string> { "cloud" }, state.PendingRunes);
            Assert.IsTrue(sim.Events.Exists(e => e.Type == RunEventType.RunePending && e.Id == "cloud"));
            sim.Tick(Choose(1));
            Assert.AreEqual(RunPhase.Playing, state.Phase);
            Assert.AreEqual("fire", state.Slots[1].SubstanceId);
            Assert.AreEqual("cloud", state.Slots[1].DeliveryId);
        }

        [Test]
        public void OutOfRangeChoice_IsIgnored()
        {
            DropAtPlayer("fire");
            sim.Tick(TickInput.None);
            sim.Tick(Choose(7));
            sim.Tick(Choose(-9));
            Assert.AreEqual(RunPhase.ChoosingRune, state.Phase);
            Assert.AreEqual(1, state.PendingRunes.Count);
        }

        [Test]
        public void ChoiceWhilePlaying_IsIgnored()
        {
            sim.Tick(Choose(1));
            Assert.AreEqual(RunPhase.Playing, state.Phase);
            Assert.IsNull(state.Slots[1].SubstanceId);
            Assert.AreEqual(1, state.Tick);
        }

        [Test]
        public void Drops_ComeFromStagePool()
        {
            GameData always = TestData.LoadWithBalance("rune_drop_chance", "1");
            Simulation s = SimHelpers.NewSim(always, quiet: true);
            s.State.Slots[0].DeliveryId = null;
            for (int i = 0; i < 50; i++)
                SimHelpers.AddEnemy(s, always, "grunt", new Vector2(1f + i * 0.5f, 1f)).Hp = 0f;
            s.Tick(TickInput.None);
            Assert.AreEqual(50, s.State.Pickups.Count);
            var pool = new List<string>(always.Stage("swamp").Runes);
            var seen = new HashSet<string>();
            foreach (PickupState p in s.State.Pickups)
            {
                Assert.Contains(p.RuneId, pool);
                seen.Add(p.RuneId);
            }
            Assert.Greater(seen.Count, 3);
            Assert.AreEqual(50, s.Events.FindAll(e => e.Type == RunEventType.RuneDropped).Count);
        }

        [Test]
        public void NoDrops_WhenChanceIsZero()
        {
            GameData never = TestData.LoadWithBalance("rune_drop_chance", "0");
            Simulation s = SimHelpers.NewSim(never, quiet: true);
            for (int i = 0; i < 50; i++)
                SimHelpers.AddEnemy(s, never, "grunt", new Vector2(1f + i * 0.5f, 1f)).Hp = 0f;
            s.Tick(TickInput.None);
            Assert.AreEqual(0, s.State.Pickups.Count);
        }
    }
}
