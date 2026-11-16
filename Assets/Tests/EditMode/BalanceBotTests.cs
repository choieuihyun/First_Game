using System;
using System.Collections.Generic;
using System.Numerics;
using Game.Core;
using Game.Data;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>
    /// 사람 대신 단순한 봇이 판을 끝까지 플레이해 얼마나 버티는지 잰다.
    /// 봇은 사람보다 못하거나 다르게 플레이하므로 수치는 참고용이다. 난이도가 크게 어긋났는지 잡아내는 용도다
    /// </summary>
    public class BalanceBotTests
    {
        const int MaxTicks = 30 * 60 * 40;

        /// <summary>가까운 적에게서 멀어지되 맵 가운데 쪽으로 돌면서 도망친다</summary>
        static Vector2 BotMove(Simulation sim, StageDef stage)
        {
            RunState s = sim.State;
            Vector2 flee = Vector2.Zero;
            foreach (EnemyState e in s.Enemies)
            {
                Vector2 away = s.Player.Pos - e.Pos;
                float dist = away.Length();
                if (dist > 6f || dist < 0.001f) continue;
                flee += away / dist * (6f - dist);
            }
            var center = new Vector2(stage.Width / 2f, stage.Height / 2f);
            Vector2 toCenter = center - s.Player.Pos;
            float fromCenter = toCenter.Length();
            // 벽에 몰리지 않게 가운데에서 멀어질수록 가운데 쪽으로 당긴다
            Vector2 pull = fromCenter > 0.001f ? toCenter / fromCenter * (fromCenter / 6f) : Vector2.Zero;
            // 한자리에 서 있지 않게 가운데 둘레를 돈다
            Vector2 circle = fromCenter > 0.001f ? new Vector2(-toCenter.Y, toCenter.X) / fromCenter * 0.6f : new Vector2(1f, 0f);
            Vector2 move = flee + pull + circle;
            float len = move.Length();
            return len > 0.001f ? move / len : Vector2.Zero;
        }

        /// <summary>얻은 특징을 어디에 붙일지 정한다. 빈 곳부터 채우고, 같은 것이 있으면 강화하고, 아니면 가장 약한 곳을 바꾼다</summary>
        static int BotChoose(Simulation sim, GameData data)
        {
            RunState s = sim.State;
            string trait = s.PendingTraits[0];
            bool isDelivery = data.IsDelivery(trait);
            for (int i = 0; i < s.Slots.Length; i++)
            {
                SpellSlot slot = s.Slots[i];
                if (isDelivery ? slot.DeliveryId == trait : slot.SubstanceId == trait) return i;
            }
            for (int i = 0; i < s.Slots.Length; i++)
            {
                SpellSlot slot = s.Slots[i];
                if (isDelivery ? slot.DeliveryId == null : (slot.SubstanceId == null && slot.DeliveryId != null)) return i;
            }
            if (!isDelivery)
                for (int i = 0; i < s.Slots.Length; i++)
                    if (s.Slots[i].SubstanceId == null) return i;
            int weakest = 0;
            for (int i = 1; i < s.Slots.Length; i++)
            {
                int a = isDelivery ? s.Slots[i].DeliveryLevel : s.Slots[i].SubstanceLevel;
                int b = isDelivery ? s.Slots[weakest].DeliveryLevel : s.Slots[weakest].SubstanceLevel;
                if (a < b) weakest = i;
            }
            return weakest;
        }

        /// <summary>봇으로 한 판을 끝까지 돌리고 버틴 시간 (초) 을 돌려준다</summary>
        public static float PlayOnce(GameData data, string stageId, uint seed, out RunState final)
        {
            var sim = new Simulation(data, RunFactory.NewRun(data, stageId, seed));
            StageDef stage = data.Stage(stageId);
            int guard = 0;
            while (sim.State.Phase != RunPhase.Dead && sim.State.Tick < MaxTicks && guard < MaxTicks * 2)
            {
                guard++;
                TickInput input = TickInput.None;
                if (sim.State.Phase == RunPhase.ChoosingPart) input.PartChoice = BotChoose(sim, data);
                else input.Move = BotMove(sim, stage);
                sim.Tick(input);
            }
            final = sim.State;
            return sim.State.Seconds(data);
        }

        [Test]
        public void Bot_SurvivalTime_IsInSaneRange_OnEveryStage()
        {
            GameData data = TestData.Load();
            foreach (StageDef stage in data.Stages)
            {
                var times = new List<float>();
                int reactions = 0, kills = 0, parts = 0;
                for (uint seed = 1; seed <= 7; seed++)
                {
                    times.Add(PlayOnce(data, stage.Id, seed, out RunState final));
                    reactions += final.ReactionCount;
                    kills += final.Kills;
                    foreach (SpellSlot slot in final.Slots) if (slot.DeliveryId != null) parts++;
                }
                times.Sort();
                float median = times[times.Count / 2];
                TestContext.WriteLine(string.Format("BOT {0}: 가장 짧게 {1:0.0}분, 중간 {2:0.0}분, 가장 길게 {3:0.0}분, 판당 처치 {4}, 반응 {5}, 쓰는 부위 {6:0.0}",
                    stage.Id, times[0] / 60f, median / 60f, times[times.Count - 1] / 60f, kills / 7, reactions / 7, parts / 7f));
                // 목표는 한 판 10 ~ 20분 (D12). 봇은 사람이 아니므로 넉넉하게 본다
                Assert.Greater(median, 6f * 60f, stage.Id + " 가 너무 어렵다");
                Assert.Less(median, 20f * 60f, stage.Id + " 가 너무 쉽다");
                Assert.Less(times[times.Count - 1], 30f * 60f, stage.Id + " 에서 끝나지 않는 판이 있다");
                Assert.Greater(reactions, 0, stage.Id);
            }
        }
    }
}
