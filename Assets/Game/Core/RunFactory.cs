using System.Numerics;
using Game.Data;

namespace Game.Core
{
    /// <summary>새 판의 처음 상태를 만든다</summary>
    public static class RunFactory
    {
        // 지형이 플레이어 시작점에 겹치지 않게 띄우는 거리
        const float TerrainMinDistanceFromStart = 3f;

        /// <summary>스테이지와 시드로 새 판을 만든다. 같은 시드면 같은 판이 나온다</summary>
        public static RunState NewRun(GameData data, string stageId, uint seed)
        {
            StageDef stage = data.Stage(stageId);
            var s = new RunState
            {
                Rng = new Rng(seed),
                Phase = RunPhase.Playing,
                StageId = stageId,
                NextId = 1
            };
            s.Player.Pos = new Vector2(stage.Width / 2f, stage.Height / 2f);
            s.Player.Hp = data.Balance("player_hp");

            int slotCount = (int)data.Balance("slot_count");
            s.Slots = new SpellSlot[slotCount];
            for (int i = 0; i < slotCount; i++) s.Slots[i] = new SpellSlot();
            // 처음에는 첫 세트에 스테이지가 정한 방식만 끼워져 있다
            s.Slots[0].DeliveryId = stage.StartDelivery;

            if (stage.TerrainSubstance != null)
            {
                for (int i = 0; i < stage.TerrainCount; i++)
                {
                    Vector2 pos = s.Player.Pos;
                    for (int attempt = 0; attempt < 100; attempt++)
                    {
                        pos = new Vector2(s.Rng.Range(stage.TerrainRadius, stage.Width - stage.TerrainRadius),
                            s.Rng.Range(stage.TerrainRadius, stage.Height - stage.TerrainRadius));
                        if (Vector2.Distance(pos, s.Player.Pos) >= TerrainMinDistanceFromStart) break;
                    }
                    s.Zones.Add(new ZoneState
                    {
                        Id = s.NextId++,
                        Pos = pos,
                        Radius = stage.TerrainRadius,
                        TimeLeft = -1f,
                        Damage = 0f,
                        Substance = new Held { Id = stage.TerrainSubstance, TimeLeft = -1f, Depth = 0 }
                    });
                }
            }
            return s;
        }
    }
}
