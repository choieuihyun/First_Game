using System.Collections;
using System.Threading;
using Game.App;
using Game.Core;
using Game.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    /// <summary>씬을 실제로 띄워 게임이 시작되고 에러 없이 진행되는지 확인한다</summary>
    public class BootSmokeTests
    {
        static IEnumerator LoadGame()
        {
            SceneManager.LoadScene("SampleScene");
            yield return null;
            yield return null;
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator Boots_AndAdvances_WithoutErrors()
        {
            yield return LoadGame();
            var boot = Object.FindAnyObjectByType<GameBootstrap>();
            Assert.IsNotNull(boot, "GameBootstrap 이 만들어지지 않았다");
            Assert.IsNull(boot.LoadError);
            float end = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < end) yield return null;
            Assert.Greater(boot.Sim.State.Tick, 60);
            Assert.Greater(boot.Sim.State.Enemies.Count, 0);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator LongFrame_DoesNotRunHundredsOfTicks()
        {
            yield return LoadGame();
            var boot = Object.FindAnyObjectByType<GameBootstrap>();
            Assert.IsNotNull(boot);
            yield return null;
            int before = boot.Sim.State.Tick;
            // 전화나 백그라운드에서 돌아온 것처럼 한 프레임을 2초 동안 멈춘다. 밀린 60틱이 한꺼번에 돌면 안 된다
            Thread.Sleep(2000);
            yield return null;
            yield return null;
            Assert.LessOrEqual(boot.Sim.State.Tick - before, 15);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator PartChoice_FromHud_IsNeverLost()
        {
            yield return LoadGame();
            var boot = Object.FindAnyObjectByType<GameBootstrap>();
            var hud = Object.FindAnyObjectByType<Hud>();
            RunState state = boot.Sim.State;
            // 틱이 돌지 않는 프레임에 누른 선택도 전달돼야 한다. 여러 번 되풀이해 확인한다
            for (int round = 0; round < 5; round++)
            {
                state.PendingTraits.Add("fire");
                float end = Time.realtimeSinceStartup + 2f;
                while (state.Phase != RunPhase.ChoosingPart && Time.realtimeSinceStartup < end) { state.Player.Hp = 100f; yield return null; }
                Assert.AreEqual(RunPhase.ChoosingPart, state.Phase);
                state.Slots[1].SubstanceId = null;
                hud.ChoosePart(1);
                end = Time.realtimeSinceStartup + 1f;
                while (state.Phase == RunPhase.ChoosingPart && Time.realtimeSinceStartup < end) yield return null;
                Assert.AreEqual("fire", state.Slots[1].SubstanceId, "선택이 사라졌다 (" + round + "번째)");
                Assert.AreNotEqual(RunPhase.ChoosingPart, state.Phase);
            }
        }

        static IEnumerator WaitForPhase(RunState state, RunPhase phase, float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (state.Phase != phase && Time.realtimeSinceStartup < end) yield return null;
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator PressingPartButton_AttachesTrait()
        {
            yield return LoadGame();
            var boot = Object.FindAnyObjectByType<GameBootstrap>();
            var hud = Object.FindAnyObjectByType<Hud>();
            RunState state = boot.Sim.State;
            state.SpawnTimer = 999999f;
            state.PendingTraits.Add("gas");
            yield return WaitForPhase(state, RunPhase.ChoosingPart, 2f);
            Assert.AreEqual(RunPhase.ChoosingPart, state.Phase);

            // 버튼이 없는 곳을 누르면 아무 일도 없다
            Assert.IsFalse(hud.PressAt(new Vector2(1f, 1f)));
            // 오른팔 버튼 한가운데를 누른다
            Assert.IsTrue(hud.PressAt(hud.SlotButtonRect(1).center));
            yield return WaitForPhase(state, RunPhase.Playing, 1f);
            Assert.AreEqual("gas", state.Slots[1].SubstanceId);

            state.PendingTraits.Add("water");
            yield return WaitForPhase(state, RunPhase.ChoosingPart, 2f);
            Assert.IsTrue(hud.PressAt(hud.DiscardButtonRect().center));
            yield return WaitForPhase(state, RunPhase.Playing, 1f);
            Assert.AreEqual(RunPhase.Playing, state.Phase);
            Assert.AreEqual("gas", state.Slots[1].SubstanceId);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator PressingRestart_AfterDeath_StartsNewRun()
        {
            yield return LoadGame();
            var boot = Object.FindAnyObjectByType<GameBootstrap>();
            var hud = Object.FindAnyObjectByType<Hud>();
            RunState dead = boot.Sim.State;
            float end = Time.realtimeSinceStartup + 2f;
            while (dead.Phase != RunPhase.Dead && Time.realtimeSinceStartup < end) { dead.Player.Hp = 0f; yield return null; }
            Assert.AreEqual(RunPhase.Dead, dead.Phase);
            // 살아 있을 때는 다시 시작 자리를 눌러도 반응하지 않아야 하므로 죽은 뒤에만 누른다
            Assert.IsTrue(hud.PressAt(hud.RestartButtonRect().center));
            yield return null;
            yield return null;
            Assert.AreNotSame(dead, boot.Sim.State);
            Assert.AreEqual(RunPhase.Playing, boot.Sim.State.Phase);
            Assert.AreEqual(100f, boot.Sim.State.Player.Hp);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator WhileChoosingPart_ScreenDoesNotJitter()
        {
            yield return LoadGame();
            var boot = Object.FindAnyObjectByType<GameBootstrap>();
            RunState state = boot.Sim.State;
            state.PendingTraits.Add("gas");
            yield return WaitForPhase(state, RunPhase.ChoosingPart, 2f);
            // 시간이 멈춘 동안에는 틱 사이 보간 위치가 고정돼야 한다. 흔들리면 화면이 떨린다
            for (int i = 0; i < 20; i++)
            {
                yield return null;
                Assert.AreEqual(1f, boot.RenderAlpha);
            }
        }
    }
}
