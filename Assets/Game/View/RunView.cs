using System.Collections.Generic;
using Game.Core;
using Game.Data;
using UnityEngine;
using NVec = System.Numerics.Vector2;

namespace Game.View
{
    /// <summary>판 상태를 읽어 화면에 그린다. 상태를 바꾸지 않는다</summary>
    public sealed class RunView : MonoBehaviour
    {
        // 화면 가로에 보이는 거리 (유닛). 세로 화면에서 좌우가 이만큼 보인다
        const float VisibleWidth = 9f;
        const float ExplosionSeconds = 0.35f;
        const float ChainSeconds = 0.15f;

        struct Fx
        {
            public bool IsLine;
            public Vector2 Pos;
            public Vector2 Pos2;
            public float Radius;
            public float Width;
            public float Age;
            public float Duration;
            public Color Color;
        }

        GameData data;
        Camera cam;
        Transform poolRoot;
        Transform decor;
        readonly List<SpriteRenderer> pool = new List<SpriteRenderer>();
        int cursor;
        readonly List<Fx> effects = new List<Fx>();

        // 틱 사이를 부드럽게 잇기 위해 직전 틱과 이번 틱의 위치를 기억한다
        Dictionary<int, Vector2> previous = new Dictionary<int, Vector2>();
        Dictionary<int, Vector2> current = new Dictionary<int, Vector2>();
        Vector2 previousPlayer;
        Vector2 currentPlayer;
        int lastTick = -1;

        /// <summary>그릴 준비를 한다. 카메라와 그림 풀을 만든다</summary>
        public void Init(GameData gameData)
        {
            data = gameData;
            cam = Camera.main;
            if (cam == null)
            {
                cam = new GameObject("Main Camera").AddComponent<Camera>();
                cam.tag = "MainCamera";
            }
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.09f, 0.12f, 0.13f);

            poolRoot = new GameObject("Pool").transform;
            poolRoot.SetParent(transform, false);
        }

        /// <summary>맵이 바뀌면 바닥 점과 경계를 그 맵에 맞게 다시 만든다</summary>
        public void SetStage(StageDef stage)
        {
            if (decor != null) Destroy(decor.gameObject);
            decor = new GameObject("Decor").transform;
            decor.SetParent(transform, false);
            // 지형 물질의 색을 바닥에 살짝 입혀 맵마다 분위기가 다르게 보이게 한다
            Color tint = stage.TerrainSubstance != null ? ShapeSprites.ParseColor(data.Substance(stage.TerrainSubstance).Color) : Color.white;
            cam.backgroundColor = Color.Lerp(new Color(0.08f, 0.09f, 0.1f), tint, 0.08f);
            // 움직임이 보이도록 바닥에 점을 깔아 둔다. 위치는 꾸밈이라 판의 시드와 상관없다
            var random = new System.Random(1);
            int dots = Mathf.RoundToInt(stage.Width * stage.Height * 0.4f);
            for (int i = 0; i < dots; i++)
            {
                var pos = new Vector2((float)random.NextDouble() * stage.Width, (float)random.NextDouble() * stage.Height);
                MakeStatic(decor, "Dot", "circle", new Color(1f, 1f, 1f, 0.1f), pos, new Vector2(0.08f, 0.08f), -10);
            }
            var border = new Color(1f, 1f, 1f, 0.5f);
            MakeStatic(decor, "Border", "square", border, new Vector2(stage.Width / 2f, 0f), new Vector2(stage.Width, 0.12f), -9);
            MakeStatic(decor, "Border", "square", border, new Vector2(stage.Width / 2f, stage.Height), new Vector2(stage.Width, 0.12f), -9);
            MakeStatic(decor, "Border", "square", border, new Vector2(0f, stage.Height / 2f), new Vector2(0.12f, stage.Height), -9);
            MakeStatic(decor, "Border", "square", border, new Vector2(stage.Width, stage.Height / 2f), new Vector2(0.12f, stage.Height), -9);
            Clear();
        }

        static void MakeStatic(Transform parent, string name, string shape, Color color, Vector2 pos, Vector2 scale, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = ShapeSprites.Get(shape);
            sr.color = color;
            sr.sortingOrder = order;
        }

        /// <summary>새 판을 시작할 때 기억해 둔 위치와 이펙트를 버린다</summary>
        public void Clear()
        {
            previous.Clear();
            current.Clear();
            effects.Clear();
            lastTick = -1;
        }

        /// <summary>이번 틱에 일어난 일로 이펙트를 만든다</summary>
        public void Play(IReadOnlyList<RunEvent> events)
        {
            for (int i = 0; i < events.Count; i++)
            {
                RunEvent e = events[i];
                if (e.Type == RunEventType.Explosion)
                    effects.Add(new Fx { Pos = V(e.Pos), Radius = e.Radius, Duration = ExplosionSeconds, Color = new Color(1f, 0.75f, 0.35f) });
                else if (e.Type == RunEventType.ChainLink)
                    effects.Add(new Fx { IsLine = true, Pos = V(e.Pos), Pos2 = V(e.Pos2), Width = 0.12f, Duration = ChainSeconds, Color = new Color(1f, 0.95f, 0.3f) });
                else if (e.Type == RunEventType.Beam)
                    effects.Add(new Fx { IsLine = true, Pos = V(e.Pos), Pos2 = V(e.Pos2), Width = Mathf.Max(0.12f, e.Radius * 2f), Duration = 0.2f, Color = SubstanceColor(e.Id) });
                else if (e.Type == RunEventType.Nova)
                    effects.Add(new Fx { Pos = V(e.Pos), Radius = e.Radius, Duration = 0.3f, Color = SubstanceColor(e.Id) });
            }
        }

        /// <summary>판 상태를 그린다. alpha 는 직전 틱에서 이번 틱 사이 어디쯤인지 (0 에서 1)</summary>
        public void Sync(RunState state, float alpha)
        {
            alpha = Mathf.Clamp01(alpha);
            if (state.Tick != lastTick) Remember(state);

            cursor = 0;
            Vector2 playerPos = Vector2.Lerp(previousPlayer, currentPlayer, alpha);

            foreach (ZoneState z in state.Zones)
            {
                SubstanceDef sub = z.Substance.IsEmpty ? null : data.Substance(z.Substance.Id);
                Color color = sub != null ? ShapeSprites.ParseColor(sub.Color) : new Color(0.7f, 0.7f, 0.7f);
                Draw("circle", WithAlpha(color, 0.25f), V(z.Pos), z.Radius * 2f, -5);
                Draw("ring", WithAlpha(color, 0.6f), V(z.Pos), z.Radius * 2f, -4);
                if (sub != null) Draw(sub.Shape, WithAlpha(color, 0.9f), V(z.Pos), 0.55f, -3);
            }

            foreach (EnemyState e in state.Enemies)
            {
                EnemyDef def = data.Enemy(e.DefId);
                Vector2 pos = Interpolated(e.Id, V(e.Pos), alpha);
                // 몸 종류는 윤곽 모양으로, 처음부터 지닌 물질은 몸 색으로 보여 준다
                string bodyColor = e.InnateSubstance != null ? data.Substance(e.InnateSubstance).Color : def.Color;
                Draw(def.Shape, ShapeSprites.ParseColor(bodyColor), pos, e.Radius * 2.2f, 2);
                // 얼어붙은 적은 얼음 껍질에 갇힌 것처럼 보인다
                if (e.FreezeTime > 0f) Draw("hex", new Color(0.75f, 0.95f, 1f, 0.55f), pos, e.Radius * 3f, 3);
                if (!e.Substance.IsEmpty)
                {
                    // 묻은 물질은 색과 모양 둘 다로 보여 준다
                    SubstanceDef sub = data.Substance(e.Substance.Id);
                    Draw(sub.Shape, ShapeSprites.ParseColor(sub.Color), pos + new Vector2(0f, e.Radius + 0.25f), 0.38f, 3);
                }
            }

            foreach (ProjectileState p in state.Projectiles)
            {
                Vector2 pos = Interpolated(p.Id, V(p.Pos), alpha);
                if (p.Substance.IsEmpty) Draw("circle", Color.white, pos, 0.3f, 4);
                else
                {
                    SubstanceDef sub = data.Substance(p.Substance.Id);
                    Draw(sub.Shape, ShapeSprites.ParseColor(sub.Color), pos, 0.5f, 4);
                }
            }

            DrawPlayer(state, playerPos);
            DrawEffects();

            for (int i = cursor; i < pool.Count; i++)
                if (pool[i].gameObject.activeSelf) pool[i].gameObject.SetActive(false);

            cam.orthographicSize = VisibleWidth / Mathf.Max(0.1f, cam.aspect) / 2f;
            cam.transform.position = new Vector3(playerPos.x, playerPos.y, -10f);
        }

        /// <summary>내 몸을 그린다. 앞을 향해 돌아가고, 왼팔과 오른팔과 꼬리에 붙은 것이 그 자리에 보인다</summary>
        void DrawPlayer(RunState state, Vector2 pos)
        {
            Vector2 forward = V(state.Player.Facing);
            if (forward.sqrMagnitude < 0.0001f) forward = Vector2.up;
            forward.Normalize();
            var left = new Vector2(-forward.y, forward.x);

            Draw("circle", new Color(0.95f, 0.95f, 1f), pos, 0.7f, 5);
            // 어디가 앞인지 알 수 있게 작은 코를 단다
            Draw("circle", new Color(0.95f, 0.95f, 1f), pos + forward * 0.36f, 0.26f, 5);

            DrawPart(state.Slots[BodyPart.LeftArm], pos + left * 0.52f);
            DrawPart(state.Slots[BodyPart.RightArm], pos - left * 0.52f);
            DrawPart(state.Slots[BodyPart.Tail], pos - forward * 0.58f);
        }

        void DrawPart(SpellSlot slot, Vector2 pos)
        {
            if (slot.DeliveryId == null && slot.SubstanceId == null) return;
            Color color = slot.SubstanceId != null
                ? ShapeSprites.ParseColor(data.Substance(slot.SubstanceId).Color)
                : new Color(0.75f, 0.75f, 0.8f);
            if (slot.DeliveryId == null)
            {
                // 몸은 아직 없고 물질만 붙은 자리는 작은 점으로 보인다
                Draw("circle", color, pos, 0.16f, 6);
                return;
            }
            EnemyDef body = data.BodyForDelivery(slot.DeliveryId);
            int extra = Mathf.Max(0, slot.DeliveryLevel - 1) + Mathf.Max(0, slot.SubstanceLevel - 1);
            // 강화할수록 그 부위가 커진다
            Draw(body != null ? body.Shape : "circle", color, pos, 0.34f * (1f + 0.1f * extra), 6);
        }

        void Remember(RunState state)
        {
            bool first = lastTick < 0;
            lastTick = state.Tick;
            Dictionary<int, Vector2> swap = previous;
            previous = current;
            current = swap;
            current.Clear();
            foreach (EnemyState e in state.Enemies) current[e.Id] = V(e.Pos);
            foreach (ProjectileState p in state.Projectiles) current[p.Id] = V(p.Pos);
            previousPlayer = first ? V(state.Player.Pos) : currentPlayer;
            currentPlayer = V(state.Player.Pos);
        }

        Vector2 Interpolated(int id, Vector2 now, float alpha)
        {
            return previous.TryGetValue(id, out Vector2 before) ? Vector2.Lerp(before, now, alpha) : now;
        }

        void DrawEffects()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = effects.Count - 1; i >= 0; i--)
            {
                Fx fx = effects[i];
                fx.Age += dt;
                if (fx.Age >= fx.Duration) { effects.RemoveAt(i); continue; }
                effects[i] = fx;
                float t = fx.Age / fx.Duration;
                if (fx.IsLine)
                {
                    Vector2 d = fx.Pos2 - fx.Pos;
                    SpriteRenderer line = Draw("square", WithAlpha(fx.Color, 1f - t), (fx.Pos + fx.Pos2) / 2f, 1f, 6);
                    line.transform.localScale = new Vector3(d.magnitude / 0.9f, fx.Width, 1f);
                    line.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                }
                else
                {
                    float size = fx.Radius * 2f * (0.35f + 0.65f * t);
                    Draw("circle", WithAlpha(fx.Color, 0.45f * (1f - t)), fx.Pos, size, 6);
                    Draw("thick_ring", WithAlpha(Color.white, 1f - t), fx.Pos, size, 7);
                }
            }
        }

        SpriteRenderer Draw(string shape, Color color, Vector2 pos, float scale, int order)
        {
            if (cursor == pool.Count)
            {
                var go = new GameObject("Sprite");
                go.transform.SetParent(poolRoot, false);
                pool.Add(go.AddComponent<SpriteRenderer>());
            }
            SpriteRenderer sr = pool[cursor++];
            if (!sr.gameObject.activeSelf) sr.gameObject.SetActive(true);
            sr.sprite = ShapeSprites.Get(shape);
            sr.color = color;
            sr.sortingOrder = order;
            Transform t = sr.transform;
            t.position = pos;
            t.rotation = Quaternion.identity;
            t.localScale = new Vector3(scale, scale, 1f);
            return sr;
        }

        Color SubstanceColor(string substanceId)
        {
            return substanceId != null ? ShapeSprites.ParseColor(data.Substance(substanceId).Color) : Color.white;
        }

        static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }

        static Vector2 V(NVec v)
        {
            return new Vector2(v.X, v.Y);
        }
    }
}
