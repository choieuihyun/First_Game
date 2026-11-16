using Game.Core;
using Game.Data;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.View
{
    /// <summary>임시 화면 글자와 버튼. 체력, 포식 게이지, 신체 부위, 부위 고르기, 결과 화면을 보여 준다. 정식 UI 로 바꿀 예정</summary>
    public sealed class Hud : MonoBehaviour
    {
        // 화면 가로를 이 값으로 보고 배치한다. 기기 해상도와 상관없이 같은 비율로 보인다
        const float DesignWidth = 540f;
        const float ReactionShowSeconds = 2f;
        const int GaugesPerRow = 4;

        static readonly string[] PartNames = { "왼팔", "오른팔", "꼬리" };

        GameData data;
        GUIStyle label;
        GUIStyle small;
        GUIStyle title;
        GUIStyle button;
        int pendingChoice = TickInput.NoChoice;
        bool pendingRestart;
        string lastReaction;
        float lastReactionTime = -100f;

        /// <summary>그릴 판. 없으면 에러 글자만 그린다</summary>
        public Simulation Sim;
        /// <summary>데이터를 못 읽었을 때 보여 줄 글자</summary>
        public string ErrorText;
        public bool Paused;

        RunState State => Sim != null ? Sim.State : null;

        public void Init(GameData gameData)
        {
            data = gameData;
        }

        /// <summary>방금 일어난 반응 이름을 잠깐 보여 준다</summary>
        public void NoteReaction(string reactionId)
        {
            if (data == null) return;
            foreach (ReactionDef r in data.Reactions)
                if (r.Id == reactionId) lastReaction = r.Name;
            lastReactionTime = Time.unscaledTime;
        }

        /// <summary>붙일 부위 선택을 기록한다. 부위 번호 또는 TickInput.Discard</summary>
        public void ChoosePart(int choice)
        {
            pendingChoice = choice;
        }

        /// <summary>플레이어가 누른 부위 선택을 한 번만 돌려준다</summary>
        public int ConsumePartChoice()
        {
            int choice = pendingChoice;
            pendingChoice = TickInput.NoChoice;
            return choice;
        }

        /// <summary>다시 시작 버튼을 눌렀는지 한 번만 돌려준다</summary>
        public bool ConsumeRestart()
        {
            bool restart = pendingRestart;
            pendingRestart = false;
            return restart;
        }

        // 화면 배치는 가로 DesignWidth 기준 좌표로 한다. 왼쪽 위가 원점이다
        float DesignScale => Screen.width / DesignWidth;
        float DesignHeight => Screen.height / DesignScale;
        float ChoicePanelTop => DesignHeight / 2f - 230f;
        float ResultPanelTop => DesignHeight / 2f - 160f;

        /// <summary>특징을 붙일 부위 버튼의 자리</summary>
        public Rect SlotButtonRect(int index)
        {
            return new Rect(40f, ChoicePanelTop + 95f + index * 88f, DesignWidth - 80f, 78f);
        }

        /// <summary>특징 버리기 버튼의 자리</summary>
        public Rect DiscardButtonRect()
        {
            return new Rect(40f, ChoicePanelTop + 95f + BodyPart.Count * 88f, DesignWidth - 80f, 60f);
        }

        /// <summary>다시 시작 버튼의 자리</summary>
        public Rect RestartButtonRect()
        {
            return new Rect(40f, ResultPanelTop + 220f, DesignWidth - 80f, 70f);
        }

        /// <summary>배치 좌표의 한 점을 눌렀을 때의 처리. 버튼을 눌렀으면 true</summary>
        public bool PressAt(Vector2 designPoint)
        {
            RunState state = State;
            if (state == null) return false;
            if (state.Phase == RunPhase.ChoosingPart && state.PendingTraits.Count > 0)
            {
                for (int i = 0; i < state.Slots.Length; i++)
                {
                    if (!SlotButtonRect(i).Contains(designPoint)) continue;
                    ChoosePart(i);
                    return true;
                }
                if (DiscardButtonRect().Contains(designPoint)) { ChoosePart(TickInput.Discard); return true; }
            }
            else if (state.Phase == RunPhase.Dead && RestartButtonRect().Contains(designPoint))
            {
                pendingRestart = true;
                return true;
            }
            return false;
        }

        // 이 프로젝트는 새 입력 방식만 켜져 있어 OnGUI 버튼이 터치를 받지 못한다
        // 그래서 버튼은 그림만 그리고, 누름은 여기서 직접 읽어 판정한다
        void Update()
        {
            RunState state = State;
            if (state == null) return;

            Pointer pointer = Pointer.current;
            if (pointer != null && pointer.press.wasPressedThisFrame)
            {
                Vector2 screen = pointer.position.ReadValue();
                PressAt(new Vector2(screen.x / DesignScale, (Screen.height - screen.y) / DesignScale));
            }

            Keyboard kb = Keyboard.current;
            if (kb == null) return;
            if (state.Phase == RunPhase.ChoosingPart)
            {
                // 에디터에서 편하게 쓰는 키. 1 왼팔, 2 오른팔, 3 꼬리, X 버리기
                if (kb.digit1Key.wasPressedThisFrame) ChoosePart(BodyPart.LeftArm);
                if (kb.digit2Key.wasPressedThisFrame) ChoosePart(BodyPart.RightArm);
                if (kb.digit3Key.wasPressedThisFrame) ChoosePart(BodyPart.Tail);
                if (kb.xKey.wasPressedThisFrame) ChoosePart(TickInput.Discard);
            }
            else if (state.Phase == RunPhase.Dead && (kb.rKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame))
            {
                pendingRestart = true;
            }
        }

        /// <summary>특징의 화면 이름. 방식은 그것을 지닌 몸 종류 이름으로 부른다</summary>
        string TraitName(string trait)
        {
            if (!data.IsDelivery(trait)) return data.Substance(trait).Name;
            EnemyDef body = data.BodyForDelivery(trait);
            return (body != null ? body.Name + "의 몸" : "몸") + " (" + data.Delivery(trait).Name + ")";
        }

        string ShortTraitName(string trait)
        {
            return data.IsDelivery(trait) ? data.Delivery(trait).Name : data.Substance(trait).Name;
        }

        string PartText(SpellSlot slot)
        {
            if (slot.DeliveryId == null && slot.SubstanceId == null) return "비어 있음";
            string delivery = slot.DeliveryId == null
                ? "몸 없음 (아직 못 쏨)"
                : data.Delivery(slot.DeliveryId).Name + " " + Mathf.Max(1, slot.DeliveryLevel) + "단계";
            string substance = slot.SubstanceId == null
                ? "물질 없음"
                : data.Substance(slot.SubstanceId).Name + " " + Mathf.Max(1, slot.SubstanceLevel) + "단계";
            return delivery + " + " + substance;
        }

        /// <summary>이 특징을 그 부위에 붙이면 무슨 일이 일어나는지</summary>
        string ChoiceText(int part, string trait)
        {
            SpellSlot slot = Sim.State.Slots[part];
            bool isDelivery = data.IsDelivery(trait);
            bool same = isDelivery ? slot.DeliveryId == trait : slot.SubstanceId == trait;
            string head;
            if (same)
            {
                int level = Mathf.Max(1, isDelivery ? slot.DeliveryLevel : slot.SubstanceLevel);
                int max = (int)data.Balance("upgrade_max");
                head = level >= max
                    ? PartNames[part] + " (이미 최대 단계)"
                    : PartNames[part] + " 강화 (" + level + "단계 → " + (level + 1) + "단계)";
            }
            else head = PartNames[part] + "에 붙이기";
            return head + "\n지금: " + PartText(slot);
        }

        void OnGUI()
        {
            float scale = DesignScale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float w = DesignWidth;
            float h = DesignHeight;

            if (label == null)
            {
                label = new GUIStyle(GUI.skin.label) { fontSize = 20, wordWrap = true };
                label.normal.textColor = Color.white;
                small = new GUIStyle(label) { fontSize = 16, wordWrap = false };
                title = new GUIStyle(label) { fontSize = 28, alignment = TextAnchor.MiddleCenter };
                button = new GUIStyle(GUI.skin.button) { fontSize = 22, wordWrap = true };
            }

            if (!string.IsNullOrEmpty(ErrorText))
            {
                Box(new Rect(20, h / 2 - 200, w - 40, 400));
                GUI.Label(new Rect(40, h / 2 - 180, w - 80, 360), "게임을 진행할 수 없습니다\n\n" + ErrorText, label);
                return;
            }
            RunState state = State;
            if (state == null || data == null) return;

            float hpMax = data.Balance("player_hp");
            GUI.color = new Color(0f, 0f, 0f, 0.5f);
            GUI.DrawTexture(new Rect(16, 40, w - 32, 22), Texture2D.whiteTexture);
            GUI.color = new Color(0.9f, 0.25f, 0.3f);
            GUI.DrawTexture(new Rect(16, 40, (w - 32) * Mathf.Clamp01(state.Player.Hp / hpMax), 22), Texture2D.whiteTexture);
            GUI.color = Color.white;

            int seconds = (int)state.Seconds(data);
            GUI.Label(new Rect(16, 66, w - 32, 28),
                string.Format("{0:00}:{1:00}   먹은 수 {2}   반응 {3}", seconds / 60, seconds % 60, state.Kills, state.ReactionCount), label);
            DrawGauges(w, state);
            if (lastReaction != null && Time.unscaledTime - lastReactionTime < ReactionShowSeconds)
                GUI.Label(new Rect(0, 160, w, 40), lastReaction + "!", title);

            Box(new Rect(0, h - 110, w, 110));
            for (int i = 0; i < state.Slots.Length; i++)
                GUI.Label(new Rect(16, h - 104 + i * 32, w - 32, 30), PartNames[i] + "  " + PartText(state.Slots[i]), label);

            if (state.Phase == RunPhase.ChoosingPart && state.PendingTraits.Count > 0) DrawPartChoice(w, state);
            else if (state.Phase == RunPhase.Dead) DrawResult(w, seconds, state);
            else if (Paused) GUI.Label(new Rect(0, h / 2 - 30, w, 60), "일시정지", title);
        }

        /// <summary>포식 게이지. 특징마다 지금까지 먹은 수와 필요한 수, 그리고 막대</summary>
        void DrawGauges(float w, RunState state)
        {
            StageDef stage = data.Stage(state.StageId);
            float cell = (w - 32f) / GaugesPerRow;
            for (int i = 0; i < stage.Absorb.Count && i < state.AbsorbCount.Length; i++)
            {
                string trait = stage.Absorb[i].Key;
                int need = Sim.AbsorbNeed(i);
                float x = 16f + (i % GaugesPerRow) * cell;
                float y = 98f + (i / GaugesPerRow) * 28f;
                Color color = data.IsDelivery(trait) ? new Color(0.8f, 0.8f, 0.85f) : ShapeSprites.ParseColor(data.Substance(trait).Color);
                GUI.color = new Color(0f, 0f, 0f, 0.5f);
                GUI.DrawTexture(new Rect(x, y + 20f, cell - 8f, 5f), Texture2D.whiteTexture);
                GUI.color = color;
                GUI.DrawTexture(new Rect(x, y + 20f, (cell - 8f) * Mathf.Clamp01(state.AbsorbCount[i] / (float)need), 5f), Texture2D.whiteTexture);
                GUI.Label(new Rect(x, y - 2f, cell, 22f), ShortTraitName(trait) + " " + state.AbsorbCount[i] + "/" + need, small);
                GUI.color = Color.white;
            }
        }

        void DrawPartChoice(float w, RunState state)
        {
            float top = ChoicePanelTop;
            string trait = state.PendingTraits[0];
            Box(new Rect(20, top, w - 40, 460));
            GUI.Label(new Rect(20, top + 10, w - 40, 50), "흡수: " + TraitName(trait), title);
            if (state.PendingTraits.Count > 1)
                GUI.Label(new Rect(40, top + 60, w - 80, 28), "남은 특징 " + (state.PendingTraits.Count - 1) + "개", label);
            // 버튼은 그림만 그린다. 누름 판정은 PressAt 이 같은 자리로 한다
            for (int i = 0; i < state.Slots.Length; i++)
                GUI.Box(SlotButtonRect(i), ChoiceText(i, trait), button);
            GUI.Box(DiscardButtonRect(), "버리기", button);
        }

        void DrawResult(float w, int seconds, RunState state)
        {
            float top = ResultPanelTop;
            Box(new Rect(20, top, w - 40, 320));
            GUI.Label(new Rect(20, top + 10, w - 40, 60), "쓰러졌습니다", title);
            GUI.Label(new Rect(40, top + 80, w - 80, 120),
                string.Format("버틴 시간 {0:00}:{1:00}\n먹은 수 {2}\n반응 {3}", seconds / 60, seconds % 60, state.Kills, state.ReactionCount), label);
            GUI.Box(RestartButtonRect(), "다시 시작", button);
        }

        static void Box(Rect rect)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}
