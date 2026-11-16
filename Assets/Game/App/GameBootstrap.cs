using System;
using Game.Core;
using Game.Data;
using Game.View;
using UnityEngine;
using UnityEngine.SceneManagement;
using NVec = System.Numerics.Vector2;

namespace Game.App
{
    /// <summary>게임의 시작점. 데이터를 읽고 판을 만들고 정해진 간격으로 진행시킨다. 씬에 아무것도 배치하지 않아도 스스로 뜬다</summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        const string SceneName = "SampleScene";
        // 한 프레임에 돌리는 틱 수 상한. 앱이 멈췄다 돌아왔을 때 밀린 시간이 한꺼번에 돌지 않게 한다
        const int MaxTicksPerFrame = 5;

        GameData data;
        RunView view;
        Hud hud;
        InputReader input;
        float tickSeconds;
        float accumulated;
        // 누른 부위 선택을 틱에 전달할 때까지 쥐고 있는다. 틱이 돌지 않는 프레임에 눌러도 사라지지 않게 한다
        int heldChoice = TickInput.NoChoice;
        string stageId;

        /// <summary>지금 진행 중인 판</summary>
        public Simulation Sim { get; private set; }
        /// <summary>데이터를 읽지 못했을 때의 메시지. 정상이면 null</summary>
        public string LoadError { get; private set; }
        public bool Paused { get; private set; }
        /// <summary>화면이 직전 틱과 이번 틱 사이 어디를 그리고 있는지 (0 에서 1). 시간이 멈춘 동안은 1</summary>
        public float RenderAlpha { get; private set; } = 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != SceneName) return;
            if (FindAnyObjectByType<GameBootstrap>() != null) return;
            new GameObject("Game").AddComponent<GameBootstrap>();
        }

        void Awake()
        {
            hud = gameObject.AddComponent<Hud>();
            try
            {
                data = GameData.Load(ReadCsv);
                tickSeconds = 1f / data.Balance("tick_rate");
                input = new InputReader();
                view = gameObject.AddComponent<RunView>();
                view.Init(data);
                hud.Init(data);
                // 판은 아직 없다. 화면에서 맵을 고르면 시작한다
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        /// <summary>데이터나 규칙 문제로 진행할 수 없을 때 죽지 않고 화면에 이유를 보여 준다</summary>
        void Fail(Exception e)
        {
            LoadError = e.Message;
            hud.ErrorText = e.Message;
            Debug.LogWarning("게임을 진행할 수 없습니다: " + e);
        }

        static string ReadCsv(string name)
        {
            TextAsset asset = Resources.Load<TextAsset>("Data/" + name);
            if (asset == null) throw new DataException(name + ".csv", "파일을 찾지 못했습니다");
            return asset.text;
        }

        /// <summary>고른 맵으로 새 판을 시작한다</summary>
        public void StartStage(string id)
        {
            if (data == null || LoadError != null) return;
            try
            {
                stageId = id;
                view.SetStage(data.Stage(id));
                Sim = new Simulation(data, RunFactory.NewRun(data, id, (uint)Environment.TickCount));
                hud.Sim = Sim;
                accumulated = 0f;
                heldChoice = TickInput.NoChoice;
                RenderAlpha = 1f;
                hud.ClearReaction();
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        void OnApplicationPause(bool paused)
        {
            SetPaused(paused);
        }

        void OnApplicationFocus(bool focused)
        {
            // 에디터에서는 다른 창을 누를 때마다 멈추면 불편하므로 폰에서만 포커스를 본다
            if (Application.isMobilePlatform) SetPaused(!focused);
        }

        void SetPaused(bool paused)
        {
            Paused = paused;
            accumulated = 0f;
            if (hud != null) hud.Paused = paused;
        }

        void Update()
        {
            if (LoadError != null || data == null) return;

            // 판이 없거나 죽은 뒤에는 맵 고르기를 받는다
            if (Sim == null || Sim.State.Phase == RunPhase.Dead)
            {
                string chosen = hud.ConsumeStageChoice();
                if (chosen != null) StartStage(chosen);
            }
            if (Sim == null) return;
            if (Paused)
            {
                RenderAlpha = 1f;
                view.Sync(Sim.State, 1f);
                return;
            }

            Vector2 move = Vector2.zero;
            if (Sim.State.Phase == RunPhase.Playing) move = input.ReadMove();
            else input.Reset();
            int pressed = hud.ConsumePartChoice();
            if (pressed != TickInput.NoChoice) heldChoice = pressed;

            accumulated += Time.unscaledDeltaTime;
            int ticks = 0;
            while (accumulated >= tickSeconds && ticks < MaxTicksPerFrame)
            {
                try
                {
                    Sim.Tick(new TickInput { Move = new NVec(move.x, move.y), PartChoice = heldChoice });
                }
                catch (Exception e)
                {
                    Fail(e);
                    return;
                }
                // 부위 선택은 한 번만 전달한다
                heldChoice = TickInput.NoChoice;
                view.Play(Sim.Events);
                foreach (RunEvent e in Sim.Events)
                    if (e.Type == RunEventType.Reaction) hud.NoteReaction(e.Id);
                accumulated -= tickSeconds;
                ticks++;
            }
            // 상한을 넘겨 밀린 시간은 버린다
            if (accumulated > tickSeconds) accumulated = 0f;

            // 부위를 고르거나 죽어서 시간이 멈춘 동안에는 보간하지 않는다. 보간하면 화면이 두 위치 사이를 오가며 떨린다
            RenderAlpha = Sim.State.Phase == RunPhase.Playing ? accumulated / tickSeconds : 1f;
            view.Sync(Sim.State, RenderAlpha);
        }
    }
}
