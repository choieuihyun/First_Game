using System.Collections.Generic;
using System.Numerics;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>공간 격자가 전수 비교와 같은 결과를 내는지 검사</summary>
    public class SpatialGridTests
    {
        static List<EnemyState> RandomEnemies(int count)
        {
            var rng = new Rng(1);
            var list = new List<EnemyState>();
            for (int i = 0; i < count; i++)
                list.Add(new EnemyState { Id = i, Pos = new Vector2(rng.Range(0f, 40f), rng.Range(0f, 40f)), Radius = 0.35f, Hp = 1f });
            return list;
        }

        [Test]
        public void Query_MatchesBruteForce()
        {
            List<EnemyState> enemies = RandomEnemies(200);
            var grid = new SpatialGrid(40f, 40f, 2f);
            grid.Rebuild(enemies);
            var rng = new Rng(2);
            var results = new List<EnemyState>();
            for (int q = 0; q < 20; q++)
            {
                var center = new Vector2(rng.Range(0f, 40f), rng.Range(0f, 40f));
                float radius = rng.Range(0.5f, 6f);
                grid.Query(center, radius, results);

                var expected = new List<int>();
                foreach (EnemyState e in enemies)
                    if (Vector2.Distance(e.Pos, center) <= radius + e.Radius) expected.Add(e.Id);
                var actual = new List<int>();
                foreach (EnemyState e in results) actual.Add(e.Id);
                Assert.AreEqual(expected, actual);
            }
        }

        [Test]
        public void Query_OutsideMap_DoesNotThrow()
        {
            var grid = new SpatialGrid(40f, 40f, 2f);
            var enemies = new List<EnemyState> { new EnemyState { Id = 0, Pos = new Vector2(-1f, -1f), Radius = 0.35f } };
            grid.Rebuild(enemies);
            var results = new List<EnemyState>();
            grid.Query(new Vector2(-5f, -5f), 3f, results);
            Assert.AreEqual(0, results.Count);
            grid.Query(new Vector2(-1f, -1f), 1f, results);
            Assert.AreEqual(1, results.Count);
        }

        [Test]
        public void Nearest_RespectsExcludeAndMaxDistance()
        {
            var grid = new SpatialGrid(40f, 40f, 2f);
            var enemies = new List<EnemyState>
            {
                new EnemyState { Id = 0, Pos = new Vector2(11f, 10f), Radius = 0.35f },
                new EnemyState { Id = 1, Pos = new Vector2(13f, 10f), Radius = 0.35f },
                new EnemyState { Id = 2, Pos = new Vector2(30f, 10f), Radius = 0.35f }
            };
            grid.Rebuild(enemies);
            var from = new Vector2(10f, 10f);
            Assert.AreEqual(0, grid.Nearest(from, 5f, null).Id);
            Assert.AreEqual(1, grid.Nearest(from, 5f, new HashSet<int> { 0 }).Id);
            Assert.IsNull(grid.Nearest(from, 5f, new HashSet<int> { 0, 1 }));
            Assert.AreEqual(2, grid.Nearest(from, 100f, new HashSet<int> { 0, 1 }).Id);
        }

        [Test]
        public void Nearest_Tie_PicksLowerId()
        {
            var grid = new SpatialGrid(40f, 40f, 2f);
            var enemies = new List<EnemyState>
            {
                new EnemyState { Id = 5, Pos = new Vector2(12f, 10f), Radius = 0.35f },
                new EnemyState { Id = 3, Pos = new Vector2(8f, 10f), Radius = 0.35f }
            };
            grid.Rebuild(enemies);
            Assert.AreEqual(3, grid.Nearest(new Vector2(10f, 10f), 5f, null).Id);
        }
    }
}
