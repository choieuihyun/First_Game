using System;
using System.Collections.Generic;
using System.Numerics;

namespace Game.Core
{
    /// <summary>적을 격자 칸에 나눠 담아 가까운 적만 빠르게 찾는다. 모든 쌍을 비교하지 않기 위한 것</summary>
    public sealed class SpatialGrid
    {
        readonly float cellSize;
        readonly int cols;
        readonly int rows;
        readonly List<EnemyState>[] cells;
        readonly List<EnemyState> scratch = new List<EnemyState>();
        // 칸을 고를 때 적 크기만큼 넉넉히 보기 위해 가장 큰 적 반지름을 기억한다
        float maxRadius;

        public SpatialGrid(float width, float height, float cellSize)
        {
            this.cellSize = cellSize;
            cols = Math.Max(1, (int)Math.Ceiling(width / cellSize));
            rows = Math.Max(1, (int)Math.Ceiling(height / cellSize));
            cells = new List<EnemyState>[cols * rows];
            for (int i = 0; i < cells.Length; i++) cells[i] = new List<EnemyState>();
        }

        // 맵 밖 좌표는 가장자리 칸으로 넣는다
        int Col(float x) => Math.Min(cols - 1, Math.Max(0, (int)Math.Floor(x / cellSize)));
        int Row(float y) => Math.Min(rows - 1, Math.Max(0, (int)Math.Floor(y / cellSize)));

        /// <summary>적 목록으로 격자를 다시 채운다. 적이 움직인 뒤에 부른다</summary>
        public void Rebuild(List<EnemyState> enemies)
        {
            for (int i = 0; i < cells.Length; i++) cells[i].Clear();
            maxRadius = 0f;
            foreach (EnemyState e in enemies)
            {
                cells[Row(e.Pos.Y) * cols + Col(e.Pos.X)].Add(e);
                if (e.Radius > maxRadius) maxRadius = e.Radius;
            }
        }

        /// <summary>원 안에 걸친 적을 찾는다. 적 반지름을 포함해 판정한다. 결과는 Id 오름차순</summary>
        public void Query(Vector2 center, float radius, List<EnemyState> results)
        {
            results.Clear();
            float reach = radius + maxRadius;
            int c0 = Col(center.X - reach), c1 = Col(center.X + reach);
            int r0 = Row(center.Y - reach), r1 = Row(center.Y + reach);
            for (int r = r0; r <= r1; r++)
            {
                for (int c = c0; c <= c1; c++)
                {
                    foreach (EnemyState e in cells[r * cols + c])
                    {
                        float limit = radius + e.Radius;
                        if (Vector2.DistanceSquared(e.Pos, center) <= limit * limit) results.Add(e);
                    }
                }
            }
            results.Sort(CompareById);
        }

        /// <summary>중심 사이 거리가 maxDistance 이하인 적 중 가장 가까운 적. 거리가 같으면 Id 가 작은 쪽. 없으면 null</summary>
        public EnemyState Nearest(Vector2 from, float maxDistance, HashSet<int> excludeIds)
        {
            Query(from, maxDistance, scratch);
            EnemyState best = null;
            float bestSq = maxDistance * maxDistance;
            foreach (EnemyState e in scratch)
            {
                if (excludeIds != null && excludeIds.Contains(e.Id)) continue;
                float sq = Vector2.DistanceSquared(e.Pos, from);
                if (sq > maxDistance * maxDistance) continue;
                // scratch 가 Id 오름차순이라 같은 거리면 먼저 나온 쪽을 유지한다
                if (best == null || sq < bestSq) { best = e; bestSq = sq; }
            }
            return best;
        }

        static int CompareById(EnemyState a, EnemyState b) => a.Id.CompareTo(b.Id);
    }
}
