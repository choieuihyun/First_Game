using System.Numerics;
using Game.Core;
using Game.Data;

namespace Game.Tests
{
    /// <summary>시뮬레이션 테스트용 도우미</summary>
    public static class SimHelpers
    {
        /// <summary>늪지 맵으로 새 판을 만든다. quiet 면 적 자동 생성을 막고 지형을 치운다</summary>
        public static Simulation NewSim(GameData data, uint seed = 1, bool quiet = false)
        {
            var sim = new Simulation(data, RunFactory.NewRun(data, "swamp", seed));
            if (quiet)
            {
                sim.State.SpawnTimer = 999999f;
                sim.State.Zones.Clear();
            }
            return sim;
        }

        /// <summary>적 하나를 원하는 자리에 직접 넣는다</summary>
        public static EnemyState AddEnemy(Simulation sim, GameData data, string defId, Vector2 pos, float hp = -1f)
        {
            EnemyDef def = data.Enemy(defId);
            var e = new EnemyState
            {
                Id = sim.State.NextId++,
                DefId = defId,
                Pos = pos,
                Hp = hp > 0f ? hp : def.Hp,
                Radius = def.Radius
            };
            if (def.Substance != null) e.Substance = new Held { Id = def.Substance, TimeLeft = -1f, Depth = 0 };
            sim.State.Enemies.Add(e);
            return e;
        }

        /// <summary>같은 입력으로 여러 틱을 돌린다</summary>
        public static void Run(Simulation sim, int ticks, TickInput input)
        {
            for (int i = 0; i < ticks; i++) sim.Tick(input);
        }

        public static void Run(Simulation sim, int ticks)
        {
            Run(sim, ticks, TickInput.None);
        }

        public static TickInput Move(float x, float y)
        {
            TickInput input = TickInput.None;
            input.Move = new Vector2(x, y);
            return input;
        }
    }
}
