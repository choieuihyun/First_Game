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
            // 처음에는 맵 고르기 화면이다. 테스트는 늪지로 바로 시작한다
            Object.FindAnyObjectByType<GameBootstrap>().StartStage("swamp");
            yield return null;
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator StartsOnStageSelect_AndPressingButton_StartsThatStage()
        {
            SceneManager.LoadScene("SampleScene");
            yield return null;
            yield return null;
            var boot = Object.FindAnyObjectByType<GameBootstrap>();
            var hud = Object.FindAnyObjectByType<Hud>();
            Assert.IsNotNull(boot);
            Assert.IsNull(boot.LoadError);
            Assert.IsNull(boot.Sim, "맵을 고르기 전에는 판이 없어야 한다");
            Assert.IsFalse(hud.PressAt(new Vector2(1f, 1f)));
            Assert.IsTrue(hud.PressAt(hud.StageButtonRect(1).center));
            yield return null;
            yield return null;
            Assert.IsNotNull(boot.Sim);
            Assert.AreEqual("foundry", boot.Sim.State.StageId);
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
        public IEnumerator PressingStage_AfterDeath_StartsNewRun()
        {
            yield return LoadGame();
            var boot = Object.FindAnyObjectByType<GameBootstrap>();
            var hud = Object.FindAnyObjectByType<Hud>();
            RunState dead = boot.Sim.State;
            float end = Time.realtimeSinceStartup + 2f;
            while (dead.Phase != RunPhase.Dead && Time.realtimeSinceStartup < end) { dead.Player.Hp = 0f; yield return null; }
            Assert.AreEqual(RunPhase.Dead, dead.Phase);
            // 죽은 뒤에는 결과 화면에 맵 고르기 버튼이 뜬다. 세 번째 맵을 고른다
            Assert.IsTrue(hud.PressAt(hud.StageButtonRect(2).center));
            yield return null;
            yield return null;
            Assert.AreNotSame(dead, boot.Sim.State);
            Assert.AreEqual(RunPhase.Playing, boot.Sim.State.Phase);
            Assert.AreEqual("lab", boot.Sim.State.StageId);
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

        [Test]
        public void TinyDrag_IsIgnored_SoFacingDoesNotFlip()
        {
            // 엄지가 1~2픽셀 떨리는 정도로는 움직이지도 방향이 바뀌지도 않아야 한다
            Assert.AreEqual(Vector2.zero, InputReader.ApplyDeadzone(new Vector2(0.02f, -0.03f)));
            Assert.AreEqual(Vector2.zero, InputReader.ApplyDeadzone(new Vector2(0.1f, 0.1f)));
            Assert.AreEqual(new Vector2(0.5f, 0f), InputReader.ApplyDeadzone(new Vector2(0.5f, 0f)));
            Assert.AreEqual(1f, InputReader.ApplyDeadzone(new Vector2(3f, 4f)).magnitude, 0.001f);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator ChoiceScreen_IgnoresTouches_ForAMomentAfterItAppears()
        {
            yield return LoadGame();
            var boot = Object.FindAnyObjectByType<GameBootstrap>();
            var hud = Object.FindAnyObjectByType<Hud>();
            RunState state = boot.Sim.State;
            Assert.IsFalse(hud.InputLocked);
            state.PendingTraits.Add("gas");
            yield return WaitForPhase(state, RunPhase.ChoosingPart, 2f);
            yield return null;
            // 이동하려던 터치가 창이 뜨는 순간 버튼을 누른 것으로 처리되면 안 된다
            Assert.IsTrue(hud.InputLocked);
            float end = Time.realtimeSinceStartup + 0.6f;
            while (Time.realtimeSinceStartup < end) yield return null;
            Assert.IsFalse(hud.InputLocked);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator ChoiceText_WarnsWhenUpgradeWouldBeLost()
        {
            yield return LoadGame();
            var boot = Object.FindAnyObjectByType<GameBootstrap>();
            var hud = Object.FindAnyObjectByType<Hud>();
            RunState state = boot.Sim.State;
            state.Slots[BodyPart.RightArm].DeliveryId = "bolt";
            state.Slots[BodyPart.RightArm].DeliveryLevel = 4;
            yield return null;
            StringAssert.Contains("사라", hud.ChoiceText(BodyPart.RightArm, "cloud"));
            StringAssert.Contains("강화", hud.ChoiceText(BodyPart.RightArm, "bolt"));
            StringAssert.DoesNotContain("사라", hud.ChoiceText(BodyPart.LeftArm, "cloud"));
        }
    }
}
