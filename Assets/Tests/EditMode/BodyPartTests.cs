using System.Collections.Generic;
using System.Numerics;
using Game.Core;
using Game.Data;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>신체 부위 검사. 왼팔은 왼쪽, 오른팔은 오른쪽의 적을 노리고 꼬리는 뒤에 남긴다</summary>
    public class BodyPartTests
    {
        GameData data;
        Simulation sim;
        RunState state;
        Vector2 player;

        [SetUp]
        public void SetUp()
        {
            data = TestData.Load();
            sim = SimHelpers.NewSim(data, quiet: true);
            state = sim.State;
            player = state.Player.Pos;
            foreach (SpellSlot slot in state.Slots) { slot.DeliveryId = null; slot.DeliveryLevel = 0; }
        }

        void Give(int part, string delivery, string substance = null, int deliveryLevel = 1, int substanceLevel = 1)
        {
            state.Slots[part].DeliveryId = delivery;
            state.Slots[part].DeliveryLevel = deliveryLevel;
            state.Slots[part].SubstanceId = substance;
            state.Slots[part].SubstanceLevel = substance == null ? 0 : substanceLevel;
        }

        EnemyState Enemy(float x, float y)
        {
            return SimHelpers.AddEnemy(sim, data, "slime", player + new Vector2(x, y), 1000f);
        }

        // 처음에는 위쪽이 앞이다. 그래서 왼쪽은 x 가 작은 쪽, 오른쪽은 x 가 큰 쪽이다

        [Test]
        public void LeftArm_OnlyHitsEnemiesOnLeft()
        {
            Give(BodyPart.LeftArm, "bolt");
            EnemyState right = Enemy(3f, 0f);
            SimHelpers.Run(sim, 20);
            Assert.AreEqual(1000f, right.Hp);
            Assert.AreEqual(0, state.Projectiles.Count);
            EnemyState left = Enemy(-3f, 0f);
            SimHelpers.Run(sim, 20);
            Assert.Less(left.Hp, 1000f);
            Assert.AreEqual(1000f, right.Hp);
        }

        [Test]
        public void RightArm_OnlyHitsEnemiesOnRight()
        {
            Give(BodyPart.RightArm, "bolt");
            EnemyState left = Enemy(-3f, 0f);
            SimHelpers.Run(sim, 20);
            Assert.AreEqual(1000f, left.Hp);
            EnemyState right = Enemy(3f, 1f);
            SimHelpers.Run(sim, 20);
            Assert.Less(right.Hp, 1000f);
        }

        [Test]
        public void EnemyStraightAhead_CanBeHitByBothArms()
        {
            Give(BodyPart.LeftArm, "bolt");
            Give(BodyPart.RightArm, "bolt");
            EnemyState ahead = Enemy(0f, 3f);
            SimHelpers.Run(sim, 12);
            Assert.AreEqual(980f, ahead.Hp, 0.001f);
        }

        [Test]
        public void TurningAround_SwapsSides()
        {
            Give(BodyPart.LeftArm, "bolt");
            EnemyState e = Enemy(4f, 0f);
            // 아래로 움직이면 아래가 앞이 되고, x 가 큰 쪽이 왼쪽이 된다
            sim.Tick(SimHelpers.Move(0f, -1f));
            Assert.AreEqual(new Vector2(0f, -1f), state.Player.Facing);
            SimHelpers.Run(sim, 15);
            Assert.Less(e.Hp, 1000f);
        }

        [Test]
        public void StandingStill_KeepsLastFacing()
        {
            sim.Tick(SimHelpers.Move(1f, 0f));
            SimHelpers.Run(sim, 10);
            Assert.AreEqual(new Vector2(1f, 0f), state.Player.Facing);
        }

        [Test]
        public void ArmPicksNearestOnItsSide()
        {
            Give(BodyPart.RightArm, "bolt");
            EnemyState nearLeft = Enemy(-1.5f, 0f);
            EnemyState farRight = Enemy(5f, 0f);
            SimHelpers.Run(sim, 25);
            Assert.AreEqual(1000f, nearLeft.Hp);
            Assert.Less(farRight.Hp, 1000f);
        }

        [Test]
        public void ArmZone_SpawnsOnTarget()
        {
            Give(BodyPart.RightArm, "cloud", "gas");
            EnemyState e = Enemy(4f, 0f);
            sim.Tick(TickInput.None);
            Assert.AreEqual(1, state.Zones.Count);
            Assert.AreEqual(e.Pos.X, state.Zones[0].Pos.X, 0.1f);
            Assert.AreEqual("gas", state.Zones[0].Substance.Id);
        }

        [Test]
        public void Tail_FiresBoltBackwards()
        {
            Give(BodyPart.Tail, "bolt");
            Enemy(0f, 5f);
            sim.Tick(TickInput.None);
            Assert.AreEqual(1, state.Projectiles.Count);
            // 위쪽이 앞이므로 꼬리 탄환은 아래로 날아간다
            Assert.Less(state.Projectiles[0].Vel.Y, 0f);
            Assert.AreEqual(0f, state.Projectiles[0].Vel.X, 0.001f);
        }

        [Test]
        public void Tail_LeavesZoneWhereIWasHalfSecondAgo()
        {
            Give(BodyPart.Tail, "pool", "water");
            state.Slots[BodyPart.Tail].Cooldown = 100f;
            // 1초 뒤 내가 가 있을 자리 근처에 적을 둔다 (꼬리는 근처에 적이 있어야 쏜다)
            Enemy(6f, 3f);
            SimHelpers.Run(sim, 30, SimHelpers.Move(1f, 0f));
            state.Slots[BodyPart.Tail].Cooldown = 0f;
            sim.Tick(SimHelpers.Move(1f, 0f));
            Assert.AreEqual(1, state.Zones.Count);
            // 초당 5 로 움직이므로 0.5초 전 자리는 2.5 뒤다
            float behind = state.Player.Pos.X - state.Zones[0].Pos.X;
            Assert.AreEqual(2.5f, behind, 0.3f);
        }

        [Test]
        public void Tail_DoesNotFire_WhenNoEnemyNearby()
        {
            Give(BodyPart.Tail, "pool", "water");
            SimHelpers.Run(sim, 30, SimHelpers.Move(1f, 0f));
            Assert.AreEqual(0, state.Zones.Count);
        }

        [Test]
        public void Upgrade_IncreasesDamageAndSize()
        {
            // 방식 3단계, 물질 2단계: 피해는 1 + 0.2 x 3, 크기는 1 + 0.1 x 2
            Give(BodyPart.RightArm, "bolt", "fire", 3, 2);
            Enemy(6f, 0f);
            sim.Tick(TickInput.None);
            ProjectileState p = state.Projectiles[0];
            Assert.AreEqual(16f, p.Damage, 0.001f);
            Assert.AreEqual(0.24f, p.Radius, 0.001f);
        }

        [Test]
        public void TrailNeverGrowsBeyondHalfSecond()
        {
            SimHelpers.Run(sim, 100, SimHelpers.Move(1f, 0f));
            Assert.AreEqual(15, state.Player.Trail.Count);
        }
    }
}
