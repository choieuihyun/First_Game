using System.Collections.Generic;
using System.Numerics;
using Game.Core;
using Game.Data;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>광선과 파동, 강화 단계별 변화, 몸 종류별 행동 검사</summary>
    public class DeliveryTests
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

        void Give(int part, string delivery, string substance = null, int level = 1)
        {
            state.Slots[part].DeliveryId = delivery;
            state.Slots[part].DeliveryLevel = level;
            state.Slots[part].SubstanceId = substance;
            state.Slots[part].SubstanceLevel = substance == null ? 0 : 1;
            state.Slots[part].Cooldown = 0f;
        }

        EnemyState Enemy(string body, float x, float y, float hp = 1000f)
        {
            return SimHelpers.AddEnemy(sim, data, body, player + new Vector2(x, y), hp);
        }

        List<RunEvent> TickCollect(int ticks = 1)
        {
            var all = new List<RunEvent>();
            for (int i = 0; i < ticks; i++) { state.Player.Hp = 100f; sim.Tick(TickInput.None); all.AddRange(sim.Events); }
            return all;
        }

        // 처음에는 위쪽이 앞이라 오른팔은 x 가 큰 쪽을 노린다

        [Test]
        public void Beam_HitsEveryEnemyOnTheLine_AtOnce()
        {
            Give(BodyPart.RightArm, "beam", "fire");
            EnemyState first = Enemy("slime", 2f, 0f);
            EnemyState second = Enemy("slime", 5f, 0f);
            EnemyState offLine = Enemy("slime", 4f, 3f);
            EnemyState tooFar = Enemy("slime", 15f, 0f);
            List<RunEvent> events = TickCollect();
            Assert.AreEqual(986f, first.Hp, 0.001f);
            Assert.AreEqual(986f, second.Hp, 0.001f);
            Assert.AreEqual(1000f, offLine.Hp);
            Assert.AreEqual(1000f, tooFar.Hp);
            Assert.AreEqual("fire", second.Substance.Id);
            Assert.AreEqual(0, state.Projectiles.Count);
            Assert.IsTrue(events.Exists(e => e.Type == RunEventType.Beam));
        }

        [Test]
        public void Beam_IgnitesGasCloudOnItsPath()
        {
            Give(BodyPart.RightArm, "beam", "fire");
            sim.Resolver.SpawnZone(player + new Vector2(3f, 0f), 1f, 20f, 0f, new Held { Id = "gas", TimeLeft = -1f });
            Enemy("slime", 6f, 0f);
            TickCollect();
            Assert.AreEqual(1, state.ReactionCount);
            Assert.AreEqual(0, state.Zones.Count);
        }

        [Test]
        public void Nova_HitsEveryoneAroundTheTarget()
        {
            Give(BodyPart.RightArm, "nova", "ice");
            EnemyState target = Enemy("slime", 3f, 0f);
            EnemyState beside = Enemy("slime", 4.2f, 0.5f);
            EnemyState outside = Enemy("slime", 3f, 4f);
            List<RunEvent> events = TickCollect();
            Assert.Less(target.Hp, 1000f);
            Assert.Less(beside.Hp, 1000f);
            Assert.AreEqual(1000f, outside.Hp);
            Assert.AreEqual("ice", beside.Substance.Id);
            Assert.IsTrue(events.Exists(e => e.Type == RunEventType.Nova && e.Radius == 2f));
        }

        [Test]
        public void TailNova_BurstsBehindMe()
        {
            Give(BodyPart.Tail, "nova");
            Enemy("slime", 0f, 4f);
            EnemyState behind = Enemy("slime", 0f, -1f);
            TickCollect();
            Assert.Less(behind.Hp, 1000f);
        }

        [Test]
        public void BoltUpgrade_AddsShots_AtLevelThreeAndFive()
        {
            Give(BodyPart.RightArm, "bolt", null, 1);
            Enemy("slime", 6f, 0f);
            sim.Tick(TickInput.None);
            Assert.AreEqual(1, state.Projectiles.Count);

            state.Projectiles.Clear();
            Give(BodyPart.RightArm, "bolt", null, 3);
            sim.Tick(TickInput.None);
            Assert.AreEqual(2, state.Projectiles.Count);
            Assert.AreNotEqual(state.Projectiles[0].Vel, state.Projectiles[1].Vel);

            state.Projectiles.Clear();
            Give(BodyPart.RightArm, "bolt", null, 5);
            sim.Tick(TickInput.None);
            Assert.AreEqual(3, state.Projectiles.Count);
        }

        [Test]
        public void ZoneUpgrade_ShortensCooldown_AtLevelThree()
        {
            Give(BodyPart.RightArm, "pool", null, 1);
            Enemy("slime", 4f, 0f);
            sim.Tick(TickInput.None);
            float normal = state.Slots[BodyPart.RightArm].Cooldown;

            Give(BodyPart.RightArm, "pool", null, 3);
            sim.Tick(TickInput.None);
            float upgraded = state.Slots[BodyPart.RightArm].Cooldown;
            Assert.AreEqual(normal * 0.85f, upgraded, 0.01f);
        }

        [Test]
        public void EveryBody_HasKnownBehavior_AndDelivery()
        {
            var behaviors = new HashSet<string>();
            foreach (EnemyDef body in data.Enemies)
            {
                Assert.Contains(body.Behavior, new List<string>(GameDataValidator.Behaviors), body.Id);
                Assert.IsTrue(data.IsDelivery(body.Delivery), body.Id);
                behaviors.Add(body.Behavior);
            }
            Assert.GreaterOrEqual(data.Enemies.Count, 5);
            Assert.GreaterOrEqual(behaviors.Count, 4);
        }

        float Travelled(string body, int ticks)
        {
            Simulation s = SimHelpers.NewSim(data, quiet: true);
            foreach (SpellSlot slot in s.State.Slots) slot.DeliveryId = null;
            EnemyState e = SimHelpers.AddEnemy(s, data, body, s.State.Player.Pos + new Vector2(18f, 0f), 1000f);
            float start = Vector2.Distance(e.Pos, s.State.Player.Pos);
            for (int i = 0; i < ticks; i++) { s.State.Player.Hp = 100f; s.Tick(TickInput.None); }
            return start - Vector2.Distance(e.Pos, s.State.Player.Pos);
        }

        [Test]
        public void ChargingBody_ClosesFasterThanItsWalkingSpeed()
        {
            // 돌진하는 몸은 걷는 속도만으로 갈 수 있는 거리보다 더 많이 다가온다
            float walked = data.Enemy("horn").Speed * 6f;
            Assert.Greater(Travelled("horn", 180), walked * 1.2f);
        }

        [Test]
        public void ZigzagBody_Approaches_ButNotInAStraightLine()
        {
            Simulation s = SimHelpers.NewSim(data, quiet: true);
            foreach (SpellSlot slot in s.State.Slots) slot.DeliveryId = null;
            EnemyState e = SimHelpers.AddEnemy(s, data, "spore", s.State.Player.Pos + new Vector2(15f, 0f), 1000f);
            float maxSideways = 0f;
            for (int i = 0; i < 150; i++)
            {
                s.State.Player.Hp = 100f;
                s.Tick(TickInput.None);
                maxSideways = System.Math.Max(maxSideways, System.Math.Abs(e.Pos.Y - s.State.Player.Pos.Y));
            }
            Assert.Greater(maxSideways, 0.5f);
            Assert.Less(Vector2.Distance(e.Pos, s.State.Player.Pos), 15f);
        }

        [Test]
        public void OrbitingBody_CirclesInsteadOfRushingIn()
        {
            Simulation s = SimHelpers.NewSim(data, quiet: true);
            foreach (SpellSlot slot in s.State.Slots) slot.DeliveryId = null;
            EnemyState e = SimHelpers.AddEnemy(s, data, "eye", s.State.Player.Pos + new Vector2(6f, 0f), 1000f);
            for (int i = 0; i < 75; i++) s.Tick(TickInput.None);
            float dist = Vector2.Distance(e.Pos, s.State.Player.Pos);
            // 2.5초 뒤: 곧장 왔다면 이미 닿았겠지만, 주위를 돌고 있어 아직 닿지 않았다
            Assert.Less(dist, 5f);
            Assert.Greater(dist, 1f);
            Assert.AreEqual(100f, s.State.Player.Hp);
            Assert.Greater(System.Math.Abs(e.Pos.Y - s.State.Player.Pos.Y), 1f);
        }

        [Test]
        public void OrbitingBody_EventuallyReachesPlayer()
        {
            Simulation s = SimHelpers.NewSim(data, quiet: true);
            foreach (SpellSlot slot in s.State.Slots) slot.DeliveryId = null;
            SimHelpers.AddEnemy(s, data, "eye", s.State.Player.Pos + new Vector2(8f, 0f), 1000f);
            for (int i = 0; i < 1800 && s.State.Player.Hp >= 100f; i++) s.Tick(TickInput.None);
            Assert.Less(s.State.Player.Hp, 100f);
        }

        [Test]
        public void UpgradedBolt_StillHitsLoneEnemyFarAway()
        {
            // 발 수가 늘어도 한 발은 조준한 방향으로 곧장 가야 한다
            Give(BodyPart.RightArm, "bolt", null, 3);
            EnemyState e = Enemy("urchin", 8f, 0f);
            e.FreezeTime = 100f;
            TickCollect(40);
            Assert.Less(e.Hp, 1000f);
        }

        [Test]
        public void SubstanceLevel_MakesReactionsStronger()
        {
            // 물질 3단계: 반응 피해가 1 + 0.2 x 2 배
            sim.Resolver.SpawnZone(player + new Vector2(10f, 0f), 1.5f, 20f, 0f, new Held { Id = "gas", TimeLeft = -1f });
            EnemyState e = Enemy("slime", 11f, 0f);
            ZoneState gas = state.Zones[state.Zones.Count - 1];
            Assert.IsTrue(sim.Resolver.ContactZone(gas, new Held { Id = "fire", TimeLeft = 3f, Level = 3 }, gas.Pos));
            TickCollect();
            Assert.AreEqual(1000f - 40f * 1.4f, e.Hp, 0.01f);
        }

        [Test]
        public void SubstanceLevel_TravelsFromPartToReaction()
        {
            Give(BodyPart.RightArm, "bolt", "fire");
            state.Slots[BodyPart.RightArm].SubstanceLevel = 4;
            Enemy("slime", 5f, 0f);
            sim.Tick(TickInput.None);
            Assert.AreEqual(4, state.Projectiles[0].Substance.Level);
        }

        [Test]
        public void OrbitingBody_ReachesStandingPlayer_WithinSixSeconds()
        {
            Simulation s = SimHelpers.NewSim(data, quiet: true);
            foreach (SpellSlot slot in s.State.Slots) slot.DeliveryId = null;
            SimHelpers.AddEnemy(s, data, "eye", s.State.Player.Pos + new Vector2(4f, 0f), 1000f);
            for (int i = 0; i < 180 && s.State.Player.Hp >= 100f; i++) s.Tick(TickInput.None);
            Assert.Less(s.State.Player.Hp, 100f);
        }

        [Test]
        public void Pull_ThenChain_FindsThePulledEnemies()
        {
            // 끌어당긴 직후의 감전이 옮겨진 자리의 적을 찾아야 한다
            Vector2 at = player + new Vector2(12.3f, 0.1f);
            EnemyState metal = SimHelpers.AddEnemy(sim, data, "slime", at, 1000f, "metal");
            EnemyState pulled = SimHelpers.AddEnemy(sim, data, "slime", at + new Vector2(3.4f, 0f), 1000f);
            sim.Resolver.ContactEnemy(metal, new Held { Id = "lightning", TimeLeft = 0.5f });
            TickCollect();
            Assert.Less(pulled.Hp, 1000f);
        }
    }
}
