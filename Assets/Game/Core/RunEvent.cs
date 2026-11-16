using System.Numerics;

namespace Game.Core
{
    /// <summary>한 틱 동안 일어난 일의 종류. 화면이 이것을 읽어 이펙트와 소리를 낸다</summary>
    public enum RunEventType
    {
        EnemyDied, PlayerDied, ZoneSpawned, Reaction, Explosion, ChainLink, RuneDropped, RunePending, RuneEquipped
    }

    /// <summary>한 틱 동안 일어난 일 하나</summary>
    public struct RunEvent
    {
        public RunEventType Type;
        /// <summary>종류에 따라 반응 id, 룬 id, 적 id, 물질 id</summary>
        public string Id;
        public Vector2 Pos;
        public Vector2 Pos2;
        public float Radius;
    }
}
