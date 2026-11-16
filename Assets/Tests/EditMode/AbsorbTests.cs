using System.Collections.Generic;
using System.Numerics;
using Game.Core;
using Game.Data;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>포식 검사. 많이 잡으면 특징을 얻고, 붙일 부위를 고르고, 같은 특징이면 강화된다</summary>
    public class AbsorbTests
    {
        GameData data;
        Simulation sim;
        RunState state;
        StageDef stage;

        [SetUp]
        public void SetUp()
        {
            data = TestData.Load();
            sim = SimHelpers.NewSim(data, quiet: true);
            state = sim.State;
            stage = data.Stage("swamp");
            state.Slots[BodyPart.RightArm].DeliveryId = null;
        }

        /// <summary>적을 멀리 만들어 바로 죽인다. 한 틱 돌리면 먹은 것으로 처리된다</summary>
        void Eat(string body, string substance, int count)
        {
            for (int i = 0; i < count; i++)
                SimHelpers.AddEnemy(sim, data, body, new Vector2(1f + i * 0.1f, 1f), -1f, substance).Hp = 0f;
            state.Player.Hp = 100f;
            sim.Tick(TickInput.None);
        }

        static TickInput Choose(int part)
        {
            TickInput input = TickInput.None;
            input.PartChoice = part;
            return input;
        }

        int Count(string trait) { return state.AbsorbCount[stage.AbsorbIndex(trait)]; }

        [Test]
        public void EatingEnemy_FillsBodyAndSubstanceGauges()
        {
            Eat("slime", "water", 3);
            Assert.AreEqual(3, Count("pool"));
            Assert.AreEqual(3, Count("water"));
            Assert.AreEqual(0, Count("bolt"));
            Assert.AreEqual(RunPhase.Playing, state.Phase);
        }

        [Test]
        public void EnemyWithoutSubstance_FillsOnlyBodyGauge()
        {
            Eat("horn", null, 2);
            Assert.AreEqual(2, Count("bolt"));
            Assert.AreEqual(0, Count("fire"));
            Assert.AreEqual(0, Count("water"));
        }

        [Test]
        public void SubstanceAppliedByPlayer_DoesNotCount()
        {
            EnemyState e = SimHelpers.AddEnemy(sim, data, "horn", new Vector2(1f, 1f));
            e.Substance = new Held { Id = "fire", TimeLeft = 3f };
            e.Hp = 0f;
            sim.Tick(TickInput.None);
            Assert.AreEqual(0, Count("fire"));
            Assert.AreEqual(1, Count("bolt"));
        }

        [Test]
        public void InnateSubstance_Counts_EvenAfterItReacted()
        {
            EnemyState e = SimHelpers.AddEnemy(sim, data, "slime", new Vector2(1f, 1f), -1f, "water");
            e.Substance = new Held();
            e.Hp = 0f;
            sim.Tick(TickInput.None);
            Assert.AreEqual(1, Count("water"));
        }

        [Test]
        public void FullGauge_GivesTrait_AndPauses()
        {
            Eat("horn", null, 5);
            Assert.AreEqual(RunPhase.ChoosingPart, state.Phase);
            Assert.AreEqual(new List<string> { "bolt" }, state.PendingTraits);
            Assert.AreEqual(0, Count("bolt"));
            Assert.IsTrue(sim.Events.Exists(e => e.Type == RunEventType.TraitPending && e.Id == "bolt"));
        }

        [Test]
        public void NeedGrowsEachTimeTraitIsAcquired()
        {
            int index = stage.AbsorbIndex("bolt");
            Assert.AreEqual(5, sim.AbsorbNeed(index));
            Eat("horn", null, 5);
            sim.Tick(Choose(TickInput.Discard));
            Assert.AreEqual(8, sim.AbsorbNeed(index));
            Eat("horn", null, 7);
            Assert.AreEqual(RunPhase.Playing, state.Phase);
            Eat("horn", null, 1);
            Assert.AreEqual(RunPhase.ChoosingPart, state.Phase);
            sim.Tick(Choose(TickInput.Discard));
            Assert.AreEqual(11, sim.AbsorbNeed(index));
        }

        [Test]
        public void ChoosingPart_AttachesTrait_AndResumes()
        {
            Eat("horn", "fire", 5);
            Assert.AreEqual(new List<string> { "bolt", "fire" }, state.PendingTraits);
            sim.Tick(Choose(BodyPart.Tail));
            Assert.AreEqual(RunPhase.ChoosingPart, state.Phase);
            Assert.IsTrue(sim.Events.Exists(e => e.Type == RunEventType.TraitPending && e.Id == "fire"));
            sim.Tick(Choose(BodyPart.LeftArm));
            Assert.AreEqual(RunPhase.Playing, state.Phase);
            Assert.AreEqual("bolt", state.Slots[BodyPart.Tail].DeliveryId);
            Assert.AreEqual(1, state.Slots[BodyPart.Tail].DeliveryLevel);
            Assert.IsNull(state.Slots[BodyPart.Tail].SubstanceId);
            Assert.AreEqual("fire", state.Slots[BodyPart.LeftArm].SubstanceId);
            Assert.AreEqual(1, state.Slots[BodyPart.LeftArm].SubstanceLevel);
        }

        [Test]
        public void SameTraitOnSamePart_Upgrades_UpToMax()
        {
            SpellSlot arm = state.Slots[BodyPart.LeftArm];
            for (int i = 0; i < 7; i++)
            {
                state.PendingTraits.Add("bolt");
                sim.Tick(TickInput.None);
                sim.Tick(Choose(BodyPart.LeftArm));
            }
            Assert.AreEqual("bolt", arm.DeliveryId);
            Assert.AreEqual(5, arm.DeliveryLevel);
        }

        [Test]
        public void DifferentTrait_Replaces_AndResetsLevel()
        {
            SpellSlot arm = state.Slots[BodyPart.LeftArm];
            arm.DeliveryId = "bolt";
            arm.DeliveryLevel = 3;
            arm.SubstanceId = "fire";
            arm.SubstanceLevel = 2;
            state.PendingTraits.Add("cloud");
            sim.Tick(TickInput.None);
            sim.Tick(Choose(BodyPart.LeftArm));
            Assert.AreEqual("cloud", arm.DeliveryId);
            Assert.AreEqual(1, arm.DeliveryLevel);
            // 방식을 바꿔도 물질과 그 단계는 그대로다
            Assert.AreEqual("fire", arm.SubstanceId);
            Assert.AreEqual(2, arm.SubstanceLevel);
        }

        [Test]
        public void WhileChoosing_TimeAndGaugesStop()
        {
            Eat("horn", null, 5);
            int tick = state.Tick;
            SimHelpers.AddEnemy(sim, data, "slime", new Vector2(1f, 1f)).Hp = 0f;
            SimHelpers.Run(sim, 10);
            Assert.AreEqual(tick, state.Tick);
            Assert.AreEqual(0, Count("pool"));
            Assert.AreEqual(RunPhase.ChoosingPart, state.Phase);
        }

        [Test]
        public void OutOfRangeChoice_IsIgnored_AndChoiceWhilePlaying_IsIgnored()
        {
            sim.Tick(Choose(BodyPart.LeftArm));
            Assert.IsNull(state.Slots[BodyPart.LeftArm].DeliveryId);
            Eat("horn", null, 5);
            sim.Tick(Choose(7));
            sim.Tick(Choose(-9));
            Assert.AreEqual(RunPhase.ChoosingPart, state.Phase);
            Assert.AreEqual(1, state.PendingTraits.Count);
        }

        [Test]
        public void BigExplosionKill_CarriesOverflowIntoNextGauge()
        {
            // 한꺼번에 12마리를 잡으면 5마리로 한 번 얻고 남은 7마리는 다음 게이지 (8마리) 로 이어진다
            Eat("horn", null, 12);
            Assert.AreEqual(1, state.PendingTraits.Count);
            Assert.AreEqual(7, Count("bolt"));
        }

        [Test]
        public void DyingOnSameTick_DeathComesFirst()
        {
            for (int i = 0; i < 5; i++) SimHelpers.AddEnemy(sim, data, "horn", new Vector2(1f, 1f)).Hp = 0f;
            state.Player.Hp = 0f;
            sim.Tick(TickInput.None);
            Assert.AreEqual(RunPhase.Dead, state.Phase);
        }
    }
}
