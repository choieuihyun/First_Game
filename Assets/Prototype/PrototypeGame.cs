// 시제품 전용 코드. 재미 확인이 끝나면 통째로 지운다
// 목적: 룬 세 칸 조합이 실제로 재밌는지, 저항과 스테이지 변화가 조합을 바꾸게 만드는지 확인
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Prototype
{
    /// <summary>시제품 전체를 코드로 만들고 돌리는 클래스. 씬에 아무것도 배치하지 않아도 Play 하면 시작된다</summary>
    public class PrototypeGame : MonoBehaviour
    {
        // 조정용 수치. 시제품이라 데이터 파일 대신 여기 모아 둔다
        const float PlayerSpeed = 5f;
        const float PlayerMaxHp = 100f;
        const float PlayerRadius = 0.4f;
        const float ContactDps = 15f;
        const float StageDuration = 90f;
        const float WaveDuration = 30f;
        const float DropChance = 0.3f;
        const float PickupRadius = 1.6f;
        const float ResistMultiplier = 0.25f;
        const int MaxEnemies = 300;
        const float CameraSize = 7f;

        // 스테이지마다 떨어지는 룬 목록. 판마다 다른 조합을 만들게 하는 핵심 장치
        static readonly RuneId[][] StagePools =
        {
            new[] { RuneId.Bolt, RuneId.Ring, RuneId.Fire, RuneId.Lightning, RuneId.Multi, RuneId.Split },
            new[] { RuneId.Beam, RuneId.Ring, RuneId.Ice, RuneId.Poison, RuneId.Homing, RuneId.Big },
            new[]
            {
                RuneId.Bolt, RuneId.Ring, RuneId.Beam, RuneId.Fire, RuneId.Ice, RuneId.Lightning,
                RuneId.Poison, RuneId.Multi, RuneId.Split, RuneId.Homing, RuneId.Big
            }
        };

        static readonly Color[] StageBackgrounds =
        {
            new Color(0.12f, 0.14f, 0.2f),
            new Color(0.1f, 0.18f, 0.16f),
            new Color(0.2f, 0.11f, 0.14f)
        };

        /// <summary>SampleScene 이 열리면 자동으로 시제품을 띄우도록 등록한다</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            // 본 게임 (Game.App) 이 같은 씬에서 뜨므로 시제품은 PROTOTYPE1 을 정의했을 때만 뜬다
#if PROTOTYPE1
            // 처음 열린 씬뿐 아니라 나중에 불러온 씬에도 반응하도록 씬 로드 이벤트에 건다
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
#endif
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "SampleScene") return;
            if (FindAnyObjectByType<PrototypeGame>() != null) return;
            new GameObject("PrototypeGame").AddComponent<PrototypeGame>();
        }

        Sprite circleSprite;
        Sprite ringSprite;
        Sprite squareSprite;
        Camera cam;
        // 재시작할 때 통째로 지우기 위한 부모
        Transform root;

        GameObject playerGo;
        Vector2 playerPos;
        float playerHp;

        readonly List<Enemy> enemies = new List<Enemy>();
        readonly List<Projectile> projectiles = new List<Projectile>();
        readonly List<RingFx> rings = new List<RingFx>();
        readonly List<FadeFx> fades = new List<FadeFx>();
        readonly List<Pickup> pickups = new List<Pickup>();
        readonly List<Delayed> delayed = new List<Delayed>();
        readonly List<Transform> dots = new List<Transform>();

        readonly HashSet<RuneId> owned = new HashSet<RuneId>();
        RuneId shape;
        RuneId element;
        RuneId modifier;

        float elapsed;
        float spawnTimer;
        float castTimer;
        int kills;
        int stageIndex;
        int waveIndex;
        // 이번 웨이브 적이 강한 원소. 이 원소로 때리면 피해가 줄고 상태이상이 안 걸린다
        RuneId resist;

        string banner;
        float bannerTime;
        bool paused;
        bool dead;

        // 조합별 사용 시간. 한 조합만 쓰게 되는지 확인하려고 기록한다
        readonly Dictionary<string, float> comboTime = new Dictionary<string, float>();

        GUIStyle labelStyle;
        GUIStyle bigStyle;
        GUIStyle buttonStyle;

        void Start()
        {
            circleSprite = MakeCircleSprite(64, 64f);
            ringSprite = MakeCircleSprite(128, 6f);
            squareSprite = MakeSquareSprite();

            cam = Camera.main;
            if (cam == null)
            {
                cam = new GameObject("Main Camera").AddComponent<Camera>();
                cam.tag = "MainCamera";
            }
            cam.orthographic = true;
            cam.orthographicSize = CameraSize;
            cam.clearFlags = CameraClearFlags.SolidColor;

            Restart();
        }

        /// <summary>판을 처음 상태로 되돌린다</summary>
        void Restart()
        {
            if (root != null) Destroy(root.gameObject);
            root = new GameObject("PrototypeRoot").transform;

            enemies.Clear();
            projectiles.Clear();
            rings.Clear();
            fades.Clear();
            pickups.Clear();
            delayed.Clear();
            dots.Clear();
            comboTime.Clear();

            owned.Clear();
            owned.Add(RuneId.Bolt);
            shape = RuneId.Bolt;
            element = RuneId.None;
            modifier = RuneId.None;

            elapsed = 0f;
            spawnTimer = 0f;
            castTimer = 0.5f;
            kills = 0;
            stageIndex = 0;
            waveIndex = 0;
            resist = RuneId.None;
            paused = false;
            dead = false;
            Time.timeScale = 1f;

            playerPos = Vector2.zero;
            playerHp = PlayerMaxHp;
            playerGo = MakeObject("Player", circleSprite, new Color(0.95f, 0.95f, 1f), 0.8f, 5);

            // 움직임이 보이도록 바닥에 점을 깔아 둔다
            for (int i = 0; i < 260; i++)
            {
                var dot = MakeObject("Dot", circleSprite, new Color(1f, 1f, 1f, 0.12f), 0.08f, -10);
                dot.transform.position = new Vector3(Random.Range(-20f, 20f), Random.Range(-20f, 20f), 0f);
                dots.Add(dot.transform);
            }

            cam.backgroundColor = StageBackgrounds[0];
            ShowBanner("스테이지 1  적을 잡아 룬을 모으세요");
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (dead && kb.rKey.wasPressedThisFrame) Restart();
                if (!dead && kb.spaceKey.wasPressedThisFrame)
                {
                    paused = !paused;
                    Time.timeScale = paused ? 0f : 1f;
                }
            }
            if (dead || paused) return;

            float dt = Time.deltaTime;
            elapsed += dt;
            if (bannerTime > 0f) bannerTime -= dt;

            UpdateStageAndWave();
            UpdatePlayer(dt, kb);
            SpawnEnemies(dt);
            UpdateEnemies(dt);
            CastSpell(dt);
            UpdateProjectiles(dt);
            UpdateRings(dt);
            UpdateFades(dt);
            UpdatePickups(dt);
            UpdateDelayed(dt);
            RemoveFinished();

            string combo = ComboName();
            comboTime.TryGetValue(combo, out float t);
            comboTime[combo] = t + dt;

            if (playerHp <= 0f)
            {
                dead = true;
                Time.timeScale = 0f;
            }
        }

        void LateUpdate()
        {
            cam.transform.position = new Vector3(playerPos.x, playerPos.y, -10f);

            // 바닥 점을 카메라 주변 40 칸 안으로 감아서 끝없는 바닥처럼 보이게 한다
            foreach (var dot in dots)
            {
                Vector2 p = dot.position;
                p.x = playerPos.x + Mathf.Repeat(p.x - playerPos.x + 20f, 40f) - 20f;
                p.y = playerPos.y + Mathf.Repeat(p.y - playerPos.y + 20f, 40f) - 20f;
                dot.position = p;
            }
        }

        // ---------- 스테이지와 웨이브 ----------

        void UpdateStageAndWave()
        {
            int newStage = Mathf.Min((int)(elapsed / StageDuration), StagePools.Length - 1);
            if (newStage != stageIndex)
            {
                stageIndex = newStage;
                cam.backgroundColor = StageBackgrounds[stageIndex];
                string names = string.Join(", ", StagePools[stageIndex].Select(Runes.NameOf));
                ShowBanner($"스테이지 {stageIndex + 1}  나오는 룬이 바뀝니다: {names}");
            }

            int newWave = (int)(elapsed / WaveDuration);
            if (newWave != waveIndex)
            {
                waveIndex = newWave;
                PickResist();
            }
        }

        /// <summary>이번 웨이브의 적 저항 원소를 고른다. 지금 쓰는 원소가 걸릴 확률을 높여 조합 변경을 유도한다</summary>
        void PickResist()
        {
            var ownedElements = owned.Where(r => Runes.SlotOf(r) == RuneSlot.Element).ToList();
            if (ownedElements.Count == 0)
            {
                resist = RuneId.None;
                ShowBanner($"웨이브 {waveIndex + 1}");
                return;
            }

            if (element != RuneId.None && Random.value < 0.5f) resist = element;
            else resist = ownedElements[Random.Range(0, ownedElements.Count)];

            string hint = resist == element ? "  지금 쓰는 원소입니다. 조합을 바꿔 보세요" : "";
            ShowBanner($"웨이브 {waveIndex + 1}  적이 {Runes.NameOf(resist)}에 강해졌습니다{hint}");
        }

        // ---------- 플레이어 ----------

        void UpdatePlayer(float dt, Keyboard kb)
        {
            Vector2 input = Vector2.zero;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) input.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) input.y -= 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) input.x -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) input.x += 1f;
            }
            if (input.sqrMagnitude > 1f) input.Normalize();
            playerPos += input * PlayerSpeed * dt;
            playerGo.transform.position = playerPos;
        }

        // ---------- 적 ----------

        void SpawnEnemies(float dt)
        {
            spawnTimer -= dt;
            if (spawnTimer > 0f) return;
            // 시간이 갈수록 자주, 한 번에 여러 마리 나온다
            spawnTimer = Mathf.Max(0.1f, 0.9f - elapsed * 0.007f);
            int count = 1 + (int)(elapsed / 60f);

            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;
            float spawnDist = Mathf.Sqrt(halfW * halfW + halfH * halfH) + 1f;

            for (int i = 0; i < count && enemies.Count < MaxEnemies; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                var e = new Enemy
                {
                    Pos = playerPos + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * spawnDist,
                    Hp = 18f + elapsed * 0.35f,
                    Speed = Random.Range(1.4f, 2f) + elapsed * 0.004f
                };
                e.Go = MakeObject("Enemy", squareSprite, Color.white, 0.7f, 1);
                e.Sr = e.Go.GetComponent<SpriteRenderer>();
                e.Go.transform.position = e.Pos;
                enemies.Add(e);
            }
        }

        void UpdateEnemies(float dt)
        {
            Color baseColor = resist == RuneId.None
                ? new Color(0.75f, 0.3f, 0.35f)
                : Color.Lerp(new Color(0.4f, 0.4f, 0.45f), Runes.ColorOf(resist), 0.75f);

            foreach (var e in enemies)
            {
                if (e.Dead) continue;

                // 지속 피해와 둔화
                if (e.BurnTime > 0f) { e.BurnTime -= dt; e.Hp -= 5f * dt; }
                if (e.PoisonTime > 0f)
                {
                    e.PoisonTime -= dt;
                    e.Hp -= 1.5f * e.PoisonStacks * dt;
                    if (e.PoisonTime <= 0f) e.PoisonStacks = 0;
                }
                if (e.SlowTime > 0f) e.SlowTime -= dt;
                if (e.FlashTime > 0f) e.FlashTime -= dt;

                Vector2 toPlayer = playerPos - e.Pos;
                float dist = toPlayer.magnitude;
                float speed = e.Speed * (e.SlowTime > 0f ? 0.45f : 1f);
                if (dist > 0.01f) e.Pos += toPlayer / dist * speed * dt;

                if (dist < PlayerRadius + e.Radius) playerHp -= ContactDps * dt;
                if (e.Hp <= 0f) Kill(e);
            }

            // 적끼리 한 덩어리로 겹치지 않게 살짝 밀어낸다
            float minDist = 0.7f;
            for (int i = 0; i < enemies.Count; i++)
            {
                var a = enemies[i];
                if (a.Dead) continue;
                for (int j = i + 1; j < enemies.Count; j++)
                {
                    var b = enemies[j];
                    if (b.Dead) continue;
                    Vector2 d = a.Pos - b.Pos;
                    float sq = d.sqrMagnitude;
                    if (sq >= minDist * minDist || sq < 0.000001f) continue;
                    float len = Mathf.Sqrt(sq);
                    Vector2 push = d / len * (minDist - len) * 0.5f;
                    a.Pos += push;
                    b.Pos -= push;
                }
            }

            foreach (var e in enemies)
            {
                if (e.Dead) continue;
                Color c = baseColor;
                if (e.BurnTime > 0f) c = Color.Lerp(c, Runes.ColorOf(RuneId.Fire), 0.4f);
                if (e.SlowTime > 0f) c = Color.Lerp(c, Runes.ColorOf(RuneId.Ice), 0.4f);
                if (e.PoisonStacks > 0) c = Color.Lerp(c, Runes.ColorOf(RuneId.Poison), 0.4f);
                if (e.FlashTime > 0f) c = Color.white;
                e.Sr.color = c;
                e.Go.transform.position = e.Pos;
            }
        }

        void Kill(Enemy e)
        {
            if (e.Dead) return;
            e.Dead = true;
            kills++;
            if (Random.value < DropChance)
            {
                var pool = StagePools[stageIndex];
                SpawnPickup(e.Pos, pool[Random.Range(0, pool.Length)]);
            }
        }

        /// <summary>적에게 피해를 주고 원소 효과를 건다. chainLeft 는 번개가 더 튈 수 있는 횟수</summary>
        void HitEnemy(Enemy e, float damage, RuneId el, int chainLeft)
        {
            if (e.Dead) return;
            bool resisted = el != RuneId.None && el == resist;
            if (resisted) damage *= ResistMultiplier;

            e.Hp -= damage;
            e.FlashTime = 0.06f;

            // 저항당하면 상태이상도 막힌다. 그래야 원소를 바꿀 이유가 확실해진다
            if (!resisted)
            {
                switch (el)
                {
                    case RuneId.Fire:
                        e.BurnTime = 2f;
                        break;
                    case RuneId.Ice:
                        e.SlowTime = 1.5f;
                        break;
                    case RuneId.Poison:
                        e.PoisonStacks = Mathf.Min(e.PoisonStacks + 1, 10);
                        e.PoisonTime = 4f;
                        break;
                    case RuneId.Lightning:
                        if (chainLeft > 0)
                        {
                            var next = Nearest(e.Pos, 2.8f, e);
                            if (next != null)
                            {
                                SpawnLine(e.Pos, next.Pos, 0.08f, Runes.ColorOf(RuneId.Lightning), 0.12f);
                                HitEnemy(next, damage * 0.6f, el, chainLeft - 1);
                            }
                        }
                        break;
                }
            }

            if (e.Hp <= 0f) Kill(e);
        }

        Enemy Nearest(Vector2 from, float maxDist = float.MaxValue, Enemy except = null)
        {
            Enemy best = null;
            float bestSq = maxDist * maxDist;
            foreach (var e in enemies)
            {
                if (e.Dead || e == except) continue;
                float sq = (e.Pos - from).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = e; }
            }
            return best;
        }

        List<Enemy> NearestMany(Vector2 from, int count)
        {
            return enemies.Where(e => !e.Dead)
                .OrderBy(e => (e.Pos - from).sqrMagnitude)
                .Take(count)
                .ToList();
        }

        // ---------- 마법 ----------

        string ComboName() =>
            $"{Runes.NameOf(shape)} + {Runes.NameOf(element)} + {Runes.NameOf(modifier)}";

        void CastSpell(float dt)
        {
            castTimer -= dt;
            if (castTimer > 0f) return;
            if (Nearest(playerPos, 12f) == null) { castTimer = 0.1f; return; }

            float cooldown = shape switch
            {
                RuneId.Ring => 1.3f,
                RuneId.Beam => 1.0f,
                _ => 0.55f
            };
            bool big = modifier == RuneId.Big;
            if (big) cooldown *= 1.3f;
            castTimer = cooldown;

            float damageMul = big ? 1.6f : 1f;
            switch (shape)
            {
                case RuneId.Ring: CastRing(damageMul, big); break;
                case RuneId.Beam: CastBeam(damageMul, big); break;
                default: CastBolt(damageMul, big); break;
            }
        }

        void CastBolt(float damageMul, bool big)
        {
            var target = Nearest(playerPos);
            if (target == null) return;
            Vector2 dir = (target.Pos - playerPos).normalized;
            int count = modifier == RuneId.Multi ? 3 : 1;
            for (int i = 0; i < count; i++)
            {
                float angle = (i - (count - 1) / 2f) * 15f;
                SpawnProjectile(playerPos, Rotate(dir, angle), big ? 7f : 10f, big ? 0.32f : 0.16f,
                    10f * damageMul, element, modifier == RuneId.Homing, modifier == RuneId.Split,
                    big ? 2 : 0, 2f, null);
            }
        }

        void CastRing(float damageMul, bool big)
        {
            int count = modifier == RuneId.Multi ? 3 : 1;
            RuneId el = element;
            bool homing = modifier == RuneId.Homing;
            bool split = modifier == RuneId.Split;
            for (int i = 0; i < count; i++)
            {
                delayed.Add(new Delayed
                {
                    Time = i * 0.2f,
                    Action = () =>
                    {
                        // 추적이면 가장 가까운 적 위에서 터지고, 아니면 플레이어 둘레로 퍼진다
                        if (homing)
                        {
                            var t = Nearest(playerPos);
                            if (t == null) return;
                            SpawnRing(t.Pos, false, 1.8f * (big ? 1.7f : 1f), 12f * damageMul, el, split);
                        }
                        else
                        {
                            SpawnRing(playerPos, true, 2.6f * (big ? 1.7f : 1f), 12f * damageMul, el, split);
                        }
                    }
                });
            }
        }

        void CastBeam(float damageMul, bool big)
        {
            float length = big ? 9f : 7f;
            float width = big ? 0.9f : 0.35f;
            float damage = 14f * damageMul;
            bool split = modifier == RuneId.Split;
            int count = modifier == RuneId.Multi ? 3 : 1;

            var dirs = new List<Vector2>();
            if (modifier == RuneId.Homing)
            {
                // 추적 광선은 서로 다른 적을 하나씩 노린다
                foreach (var t in NearestMany(playerPos, 2))
                    dirs.Add((t.Pos - playerPos).normalized);
            }
            else
            {
                var t = Nearest(playerPos);
                if (t == null) return;
                Vector2 dir = (t.Pos - playerPos).normalized;
                for (int i = 0; i < count; i++)
                    dirs.Add(Rotate(dir, (i - (count - 1) / 2f) * 20f));
            }

            foreach (var d in dirs)
                FireBeam(playerPos, d, length, width, damage, element, split);
        }

        void FireBeam(Vector2 start, Vector2 dir, float length, float width, float damage, RuneId el, bool split)
        {
            Vector2 end = start + dir * length;
            foreach (var e in enemies.ToList())
            {
                if (e.Dead) continue;
                if (DistanceToSegment(e.Pos, start, end) < width * 0.5f + e.Radius)
                    HitEnemy(e, damage, el, 2);
            }
            SpawnLine(start, end, width, Runes.ColorOf(el), 0.15f);

            // 분열 광선은 끝에서 두 갈래로 갈라진다
            if (split)
            {
                FireBeam(end, Rotate(dir, 35f), length * 0.5f, width * 0.7f, damage * 0.5f, el, false);
                FireBeam(end, Rotate(dir, -35f), length * 0.5f, width * 0.7f, damage * 0.5f, el, false);
            }
        }

        void SpawnProjectile(Vector2 pos, Vector2 dir, float speed, float radius, float damage, RuneId el,
            bool homing, bool canSplit, int pierce, float life, Enemy alreadyHit)
        {
            var p = new Projectile
            {
                Pos = pos,
                Vel = dir * speed,
                Speed = speed,
                Radius = radius,
                Damage = damage,
                Element = el,
                Homing = homing,
                CanSplit = canSplit,
                Pierce = pierce,
                Life = life
            };
            if (alreadyHit != null) p.Hit.Add(alreadyHit);
            p.Go = MakeObject("Bolt", circleSprite, Runes.ColorOf(el), radius * 2f, 3);
            p.Go.transform.position = pos;
            projectiles.Add(p);
        }

        void UpdateProjectiles(float dt)
        {
            // 분열로 탄이 새로 생기면 목록이 바뀌므로 복사본으로 돈다
            foreach (var p in projectiles.ToList())
            {
                if (p.Life <= 0f) continue;
                p.Life -= dt;

                if (p.Homing)
                {
                    var t = Nearest(p.Pos, 6f);
                    if (t != null)
                    {
                        Vector2 desired = (t.Pos - p.Pos).normalized * p.Speed;
                        p.Vel = Vector2.Lerp(p.Vel, desired, 6f * dt).normalized * p.Speed;
                    }
                }
                p.Pos += p.Vel * dt;
                p.Go.transform.position = p.Pos;

                foreach (var e in enemies)
                {
                    if (e.Dead || p.Hit.Contains(e)) continue;
                    if ((e.Pos - p.Pos).sqrMagnitude > (p.Radius + e.Radius) * (p.Radius + e.Radius)) continue;

                    p.Hit.Add(e);
                    HitEnemy(e, p.Damage, p.Element, 2);
                    if (p.CanSplit) SplitBolts(p.Pos, p.Vel.normalized, 2, 40f, p.Damage * 0.5f, p.Element, e);
                    if (p.Pierce > 0) p.Pierce--;
                    else { p.Life = 0f; break; }
                }
            }
        }

        /// <summary>분열 효과. 작은 탄을 여러 갈래로 흩뿌린다. 쪼개진 탄은 다시 쪼개지지 않는다</summary>
        void SplitBolts(Vector2 pos, Vector2 dir, int count, float spread, float damage, RuneId el, Enemy except)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = count == 1 ? 0f : -spread + 2f * spread * i / (count - 1);
                SpawnProjectile(pos, Rotate(dir, angle), 9f, 0.11f, damage, el, false, false, 0, 0.8f, except);
            }
        }

        void SpawnRing(Vector2 center, bool follow, float maxRadius, float damage, RuneId el, bool split)
        {
            var r = new RingFx
            {
                Center = center,
                Follow = follow,
                MaxRadius = maxRadius,
                Duration = 0.35f,
                Damage = damage,
                Element = el,
                SplitOnEnd = split
            };
            r.Go = MakeObject("Ring", ringSprite, Runes.ColorOf(el), 0.01f, 4);
            r.Sr = r.Go.GetComponent<SpriteRenderer>();
            r.Go.transform.position = center;
            rings.Add(r);
        }

        void UpdateRings(float dt)
        {
            foreach (var r in rings)
            {
                if (r.Time >= r.Duration) continue;
                r.Time += dt;
                if (r.Follow) r.Center = playerPos;
                float radius = r.MaxRadius * Mathf.Clamp01(r.Time / r.Duration);
                r.Go.transform.position = r.Center;
                r.Go.transform.localScale = Vector3.one * (radius * 2f);

                foreach (var e in enemies.ToList())
                {
                    if (e.Dead || r.Hit.Contains(e)) continue;
                    if ((e.Pos - r.Center).magnitude > radius + e.Radius) continue;
                    r.Hit.Add(e);
                    HitEnemy(e, r.Damage, r.Element, 2);
                }

                // 분열 고리는 다 퍼지면 바깥으로 작은 탄 여섯 개를 쏜다
                if (r.Time >= r.Duration && r.SplitOnEnd)
                {
                    for (int i = 0; i < 6; i++)
                    {
                        Vector2 dir = Rotate(Vector2.right, i * 60f);
                        SpawnProjectile(r.Center + dir * r.MaxRadius, dir, 9f, 0.11f, r.Damage * 0.4f,
                            r.Element, false, false, 0, 0.8f, null);
                    }
                }
            }
        }

        // ---------- 룬 줍기 ----------

        void SpawnPickup(Vector2 pos, RuneId id)
        {
            var p = new Pickup { Pos = pos, Id = id };
            p.Go = MakeObject("Rune", squareSprite, Runes.ColorOf(id), 0.45f, 2);
            p.Go.transform.position = pos;
            p.Go.transform.rotation = Quaternion.Euler(0f, 0f, 45f);
            pickups.Add(p);
        }

        void UpdatePickups(float dt)
        {
            foreach (var p in pickups)
            {
                if (p.Taken) continue;
                float dist = (playerPos - p.Pos).magnitude;
                if (dist < PickupRadius) p.Pos = Vector2.MoveTowards(p.Pos, playerPos, 10f * dt);
                p.Go.transform.position = p.Pos;
                if (dist < 0.5f)
                {
                    p.Taken = true;
                    Collect(p.Id);
                }
            }
        }

        /// <summary>룬을 얻는다. 새 룬은 빈 칸이면 바로 끼우고, 이미 가진 룬이면 체력을 조금 회복한다</summary>
        void Collect(RuneId id)
        {
            if (!owned.Add(id))
            {
                playerHp = Mathf.Min(PlayerMaxHp, playerHp + 5f);
                return;
            }

            var slot = Runes.SlotOf(id);
            if (slot == RuneSlot.Element && element == RuneId.None) element = id;
            else if (slot == RuneSlot.Modifier && modifier == RuneId.None) modifier = id;
            ShowBanner($"새 룬: {Runes.NameOf(id)} ({Runes.SlotName(slot)})  아래 버튼으로 바꿔 끼울 수 있어요");
        }

        void Equip(RuneId id)
        {
            switch (Runes.SlotOf(id))
            {
                case RuneSlot.Shape:
                    shape = id;
                    break;
                case RuneSlot.Element:
                    element = element == id ? RuneId.None : id;
                    break;
                default:
                    modifier = modifier == id ? RuneId.None : id;
                    break;
            }
        }

        // ---------- 공통 ----------

        void UpdateFades(float dt)
        {
            foreach (var f in fades)
            {
                f.Time += dt;
                var c = f.BaseColor;
                c.a *= 1f - Mathf.Clamp01(f.Time / f.Duration);
                f.Sr.color = c;
            }
        }

        void UpdateDelayed(float dt)
        {
            // 실행 중에 목록이 바뀔 수 있어서 복사본으로 돈다
            foreach (var d in delayed.ToList())
            {
                d.Time -= dt;
                if (d.Time <= 0f) d.Action();
            }
            delayed.RemoveAll(d => d.Time <= 0f);
        }

        void RemoveFinished()
        {
            enemies.RemoveAll(e => { if (!e.Dead) return false; Destroy(e.Go); return true; });
            projectiles.RemoveAll(p => { if (p.Life > 0f) return false; Destroy(p.Go); return true; });
            rings.RemoveAll(r => { if (r.Time < r.Duration) return false; Destroy(r.Go); return true; });
            fades.RemoveAll(f => { if (f.Time < f.Duration) return false; Destroy(f.Go); return true; });
            pickups.RemoveAll(p => { if (!p.Taken) return false; Destroy(p.Go); return true; });
        }

        void SpawnLine(Vector2 a, Vector2 b, float width, Color color, float duration)
        {
            Vector2 d = b - a;
            var go = MakeObject("Line", squareSprite, color, 1f, 4);
            go.transform.position = (a + b) * 0.5f;
            go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            go.transform.localScale = new Vector3(d.magnitude, width, 1f);
            fades.Add(new FadeFx
            {
                Go = go,
                Sr = go.GetComponent<SpriteRenderer>(),
                BaseColor = color,
                Duration = duration
            });
        }

        void ShowBanner(string text)
        {
            banner = text;
            bannerTime = 3.5f;
        }

        GameObject MakeObject(string name, Sprite sprite, Color color, float scale, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            go.transform.localScale = Vector3.one * scale;
            return go;
        }

        static Vector2 Rotate(Vector2 v, float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(rad);
            float s = Mathf.Sin(rad);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 0.0001f));
            return (p - (a + ab * t)).magnitude;
        }

        /// <summary>원 그림을 코드로 만든다. thickness 가 반지름보다 작으면 테두리만 있는 고리가 된다</summary>
        static Sprite MakeCircleSprite(int size, float thickness)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            float r = size / 2f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - r;
                    float dy = y + 0.5f - r;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(r - d) * Mathf.Clamp01(d - (r - thickness));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        static Sprite MakeSquareSprite()
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var pixels = new Color32[16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
        }

        // ---------- 화면 글자와 버튼 ----------

        void OnGUI()
        {
            // 화면 크기와 상관없이 비슷한 크기로 보이게 높이 900 기준으로 맞춘다
            float scale = Screen.height / 900f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float w = Screen.width / scale;
            const float h = 900f;

            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 18 };
                labelStyle.normal.textColor = Color.white;
                bigStyle = new GUIStyle(labelStyle) { fontSize = 26, alignment = TextAnchor.MiddleCenter, wordWrap = true };
                buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 17 };
            }

            // 왼쪽 위 상태
            GUI.color = new Color(0f, 0f, 0f, 0.5f);
            GUI.DrawTexture(new Rect(10, 10, 260, 22), Texture2D.whiteTexture);
            GUI.color = new Color(0.9f, 0.25f, 0.3f);
            GUI.DrawTexture(new Rect(10, 10, 260 * Mathf.Clamp01(playerHp / PlayerMaxHp), 22), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(16, 10, 260, 24), $"체력 {Mathf.CeilToInt(Mathf.Max(0f, playerHp))}", labelStyle);

            int sec = (int)elapsed;
            GUI.Label(new Rect(10, 38, 600, 26),
                $"{sec / 60:00}:{sec % 60:00}   스테이지 {stageIndex + 1}   웨이브 {waveIndex + 1}   처치 {kills}", labelStyle);

            string resistText = resist == RuneId.None
                ? "적 저항: 없음"
                : $"적 저항: {Runes.NameOf(resist)} (이 원소는 피해 25%, 상태이상 안 걸림)";
            GUI.color = resist == RuneId.None ? Color.white : Runes.ColorOf(resist);
            GUI.Label(new Rect(10, 64, 700, 26), resistText, labelStyle);
            GUI.color = Color.white;

            if (bannerTime > 0f && !string.IsNullOrEmpty(banner))
                GUI.Label(new Rect(w / 2 - 450, 100, 900, 60), banner, bigStyle);

            DrawRunePanel(w, h);

            if (paused)
                GUI.Label(new Rect(w / 2 - 400, h / 2 - 60, 800, 80), "일시정지  룬을 바꿔 보세요  Space 로 계속", bigStyle);

            if (dead) DrawResult(w, h);
        }

        void DrawRunePanel(float w, float h)
        {
            float panelH = 160f;
            float top = h - panelH;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(0, top, w, panelH), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(16, top + 6, w - 32, 26), $"지금 마법:  {ComboName()}", labelStyle);
            GUI.Label(new Rect(w - 520, top + 6, 510, 26), "이동 WASD 또는 방향키   일시정지 Space", labelStyle);

            var slots = new[] { RuneSlot.Shape, RuneSlot.Element, RuneSlot.Modifier };
            for (int row = 0; row < slots.Length; row++)
            {
                var slot = slots[row];
                float y = top + 38 + row * 40;
                GUI.Label(new Rect(16, y + 4, 60, 26), Runes.SlotName(slot), labelStyle);

                float x = 80f;
                foreach (var id in owned.Where(r => Runes.SlotOf(r) == slot).OrderBy(r => (int)r))
                {
                    bool equipped = id == shape || id == element || id == modifier;
                    GUI.backgroundColor = equipped ? Runes.ColorOf(id) : new Color(0.35f, 0.35f, 0.4f);
                    if (GUI.Button(new Rect(x, y, 110, 34), Runes.NameOf(id), buttonStyle)) Equip(id);
                    x += 118f;
                }
                GUI.backgroundColor = Color.white;
            }
        }

        void DrawResult(float w, float h)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.8f);
            GUI.DrawTexture(new Rect(w / 2 - 330, h / 2 - 230, 660, 400), Texture2D.whiteTexture);
            GUI.color = Color.white;

            int sec = (int)elapsed;
            var lines = new List<string>
            {
                "쓰러졌습니다",
                $"버틴 시간 {sec / 60:00}:{sec % 60:00}   처치 {kills}",
                $"써 본 조합 {comboTime.Count}개",
                ""
            };
            foreach (var kv in comboTime.OrderByDescending(kv => kv.Value).Take(5))
                lines.Add($"{kv.Key}   {kv.Value:0}초");
            lines.Add("");
            lines.Add("R 키로 다시 시작");

            GUI.Label(new Rect(w / 2 - 310, h / 2 - 220, 620, 380), string.Join("\n", lines), bigStyle);
        }
    }
}
