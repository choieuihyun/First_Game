using System;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>시드 랜덤 생성기 검사</summary>
    public class RngTests
    {
        [Test]
        public void SameSeed_SameSequence()
        {
            var a = new Rng(42);
            var b = new Rng(42);
            for (int i = 0; i < 100; i++) Assert.AreEqual(a.NextUInt(), b.NextUInt());
        }

        [Test]
        public void State_RoundTrip_ContinuesSequence()
        {
            var a = new Rng(7);
            a.NextUInt();
            var b = new Rng(1) { State = a.State };
            Assert.AreEqual(a.NextUInt(), b.NextUInt());
        }

        [Test]
        public void NextFloat_InRange_AndSpread()
        {
            var r = new Rng(3);
            float min = 1f, max = 0f;
            for (int i = 0; i < 10000; i++)
            {
                float f = r.NextFloat();
                Assert.That(f, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f));
                min = Math.Min(min, f);
                max = Math.Max(max, f);
            }
            Assert.That(min, Is.LessThan(0.01f));
            Assert.That(max, Is.GreaterThan(0.99f));
        }

        [Test]
        public void NextInt_CoversAllValues()
        {
            var r = new Rng(9);
            var seen = new bool[5];
            for (int i = 0; i < 1000; i++)
            {
                int v = r.NextInt(5);
                Assert.That(v, Is.GreaterThanOrEqualTo(0).And.LessThan(5));
                seen[v] = true;
            }
            foreach (bool s in seen) Assert.IsTrue(s);
        }

        [Test]
        public void SeedZero_IsNotStuck()
        {
            var r = new Rng(0);
            Assert.AreNotEqual(r.NextUInt(), r.NextUInt());
        }
    }
}
