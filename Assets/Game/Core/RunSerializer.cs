using System;
using System.IO;
using System.Numerics;
using System.Text;

namespace Game.Core
{
    /// <summary>판 상태를 바이트로 저장하고 되살린다. 이어하기와 결정성 검사에 쓴다</summary>
    public static class RunSerializer
    {
        /// <summary>저장 형식 버전. RunState 의 필드가 바뀌면 올린다</summary>
        public const int Version = 2;

        // 깨진 파일이 엄청난 개수를 요구해 메모리를 잡아먹지 않게 하는 한도
        const int MaxCount = 100000;

        /// <summary>판 상태 전체를 바이트로 만든다</summary>
        public static byte[] Save(RunState s)
        {
            using (var stream = new MemoryStream())
            using (var w = new BinaryWriter(stream, Encoding.UTF8))
            {
                w.Write(Version);
                w.Write(s.Tick);
                w.Write(s.Rng.State);
                w.Write((int)s.Phase);
                WriteString(w, s.StageId);
                w.Write(s.NextId);
                WriteVec(w, s.Player.Pos);
                w.Write(s.Player.Hp);
                WriteVec(w, s.Player.Facing);
                w.Write(s.Player.Trail.Count);
                foreach (Vector2 t in s.Player.Trail) WriteVec(w, t);

                w.Write(s.Slots.Length);
                foreach (SpellSlot slot in s.Slots)
                {
                    WriteString(w, slot.DeliveryId);
                    WriteString(w, slot.SubstanceId);
                    w.Write(slot.Cooldown);
                    w.Write(slot.DeliveryLevel);
                    w.Write(slot.SubstanceLevel);
                }

                w.Write(s.Enemies.Count);
                foreach (EnemyState e in s.Enemies)
                {
                    w.Write(e.Id);
                    WriteString(w, e.DefId);
                    WriteVec(w, e.Pos);
                    w.Write(e.Hp);
                    w.Write(e.Radius);
                    WriteHeld(w, e.Substance);
                    WriteString(w, e.InnateSubstance);
                }

                w.Write(s.Projectiles.Count);
                foreach (ProjectileState p in s.Projectiles)
                {
                    w.Write(p.Id);
                    WriteVec(w, p.Pos);
                    WriteVec(w, p.Vel);
                    w.Write(p.Radius);
                    w.Write(p.Damage);
                    w.Write(p.RangeLeft);
                    WriteHeld(w, p.Substance);
                }

                w.Write(s.Zones.Count);
                foreach (ZoneState z in s.Zones)
                {
                    w.Write(z.Id);
                    WriteVec(w, z.Pos);
                    w.Write(z.Radius);
                    w.Write(z.TimeLeft);
                    w.Write(z.Damage);
                    w.Write(z.ContactTimer);
                    WriteHeld(w, z.Substance);
                }

                w.Write(s.PendingTraits.Count);
                foreach (string trait in s.PendingTraits) WriteString(w, trait);

                w.Write(s.AbsorbCount.Length);
                for (int i = 0; i < s.AbsorbCount.Length; i++)
                {
                    w.Write(s.AbsorbCount[i]);
                    w.Write(s.AbsorbAcquired[i]);
                }

                w.Write(s.ReactionQueue.Count);
                foreach (PendingReaction r in s.ReactionQueue)
                {
                    WriteString(w, r.ReactionId);
                    WriteVec(w, r.Pos);
                    w.Write(r.Depth);
                    w.Write(r.TargetEnemyId);
                }

                w.Write(s.SpawnTimer);
                w.Write(s.Kills);
                w.Write(s.ReactionCount);
                w.Flush();
                return stream.ToArray();
            }
        }

        /// <summary>저장한 바이트에서 판 상태를 되살린다. 버전이 다르거나 깨졌으면 InvalidDataException</summary>
        public static RunState Load(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 4) throw new InvalidDataException("저장 데이터가 비어 있습니다");
            try
            {
                using (var stream = new MemoryStream(bytes))
                using (var r = new BinaryReader(stream, Encoding.UTF8))
                {
                    int version = r.ReadInt32();
                    if (version != Version)
                        throw new InvalidDataException("저장 버전이 다릅니다: " + version + " (지금 버전 " + Version + ")");

                    var s = new RunState();
                    s.Tick = r.ReadInt32();
                    s.Rng = new Rng(1) { State = r.ReadUInt32() };
                    s.Phase = (RunPhase)r.ReadInt32();
                    s.StageId = ReadString(r);
                    s.NextId = r.ReadInt32();
                    s.Player.Pos = ReadVec(r);
                    s.Player.Hp = r.ReadSingle();
                    s.Player.Facing = ReadVec(r);
                    int trail = ReadCount(r);
                    for (int i = 0; i < trail; i++) s.Player.Trail.Add(ReadVec(r));

                    s.Slots = new SpellSlot[ReadCount(r)];
                    for (int i = 0; i < s.Slots.Length; i++)
                        s.Slots[i] = new SpellSlot
                        {
                            DeliveryId = ReadString(r), SubstanceId = ReadString(r), Cooldown = r.ReadSingle(),
                            DeliveryLevel = r.ReadInt32(), SubstanceLevel = r.ReadInt32()
                        };

                    int n = ReadCount(r);
                    for (int i = 0; i < n; i++)
                        s.Enemies.Add(new EnemyState
                        {
                            Id = r.ReadInt32(), DefId = ReadString(r), Pos = ReadVec(r), Hp = r.ReadSingle(),
                            Radius = r.ReadSingle(), Substance = ReadHeld(r), InnateSubstance = ReadString(r)
                        });

                    n = ReadCount(r);
                    for (int i = 0; i < n; i++)
                        s.Projectiles.Add(new ProjectileState
                        {
                            Id = r.ReadInt32(), Pos = ReadVec(r), Vel = ReadVec(r), Radius = r.ReadSingle(),
                            Damage = r.ReadSingle(), RangeLeft = r.ReadSingle(), Substance = ReadHeld(r)
                        });

                    n = ReadCount(r);
                    for (int i = 0; i < n; i++)
                        s.Zones.Add(new ZoneState
                        {
                            Id = r.ReadInt32(), Pos = ReadVec(r), Radius = r.ReadSingle(), TimeLeft = r.ReadSingle(),
                            Damage = r.ReadSingle(), ContactTimer = r.ReadSingle(), Substance = ReadHeld(r)
                        });

                    n = ReadCount(r);
                    for (int i = 0; i < n; i++) s.PendingTraits.Add(ReadString(r));

                    n = ReadCount(r);
                    s.AbsorbCount = new int[n];
                    s.AbsorbAcquired = new int[n];
                    for (int i = 0; i < n; i++)
                    {
                        s.AbsorbCount[i] = r.ReadInt32();
                        s.AbsorbAcquired[i] = r.ReadInt32();
                    }

                    n = ReadCount(r);
                    for (int i = 0; i < n; i++)
                        s.ReactionQueue.Add(new PendingReaction
                        {
                            ReactionId = ReadString(r), Pos = ReadVec(r), Depth = r.ReadInt32(), TargetEnemyId = r.ReadInt32()
                        });

                    s.SpawnTimer = r.ReadSingle();
                    s.Kills = r.ReadInt32();
                    s.ReactionCount = r.ReadInt32();
                    return s;
                }
            }
            catch (InvalidDataException) { throw; }
            catch (Exception e)
            {
                // 중간에 잘렸거나 값이 엉망인 경우를 한 가지 예외로 모은다
                throw new InvalidDataException("저장 데이터가 깨졌습니다: " + e.Message);
            }
        }

        static int ReadCount(BinaryReader r)
        {
            int n = r.ReadInt32();
            if (n < 0 || n > MaxCount) throw new InvalidDataException("저장 데이터의 개수가 이상합니다: " + n);
            return n;
        }

        static void WriteString(BinaryWriter w, string s)
        {
            w.Write(s != null);
            if (s != null) w.Write(s);
        }

        static string ReadString(BinaryReader r)
        {
            return r.ReadBoolean() ? r.ReadString() : null;
        }

        static void WriteVec(BinaryWriter w, Vector2 v)
        {
            w.Write(v.X);
            w.Write(v.Y);
        }

        static Vector2 ReadVec(BinaryReader r)
        {
            float x = r.ReadSingle();
            float y = r.ReadSingle();
            return new Vector2(x, y);
        }

        static void WriteHeld(BinaryWriter w, Held h)
        {
            WriteString(w, h.Id);
            w.Write(h.TimeLeft);
            w.Write(h.Depth);
        }

        static Held ReadHeld(BinaryReader r)
        {
            return new Held { Id = ReadString(r), TimeLeft = r.ReadSingle(), Depth = r.ReadInt32() };
        }
    }
}
