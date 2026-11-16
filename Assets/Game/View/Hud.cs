using Game.Core;
using Game.Data;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.View
{
    /// <summary>임시 화면 글자와 버튼. 체력, 마법 세트, 룬 고르기, 결과 화면을 보여 준다. 정식 UI 로 바꿀 예정</summary>
    public sealed class Hud : MonoBehaviour
    {
        // 화면 가로를 이 값으로 보고 배치한다. 기기 해상도와 상관없이 같은 비율로 보인다
        const float DesignWidth = 540f;
        const float ReactionShowSeconds = 2f;

        GameData data;
        GUIStyle label;
        GUIStyle title;
        GUIStyle button;
        int pendingChoice = TickInput.NoChoice;
        bool pendingRestart;
        string lastReaction;
        float lastReactionTime = -100f;

        /// <summary>그릴 판 상태. 없으면 에러 글자만 그린다</summary>
        public RunState State;
        /// <summary>데이터를 못 읽었을 때 보여 줄 글자</summary>
        public string ErrorText;
        public bool Paused;

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

        /// <summary>룬 선택을 기록한다. 화면 버튼이 부른다. 세트 번호 또는 TickInput.Discard</summary>
        public void ChooseRune(int choice)
        {
            pendingChoice = choice;
        }

        /// <summary>플레이어가 누른 룬 선택을 한 번만 돌려준다</summary>
        public int ConsumeRuneChoice()
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
        float RunePanelTop => DesignHeight / 2f - 230f;
        float ResultPanelTop => DesignHeight / 2f - 160f;

        /// <summary>룬을 끼울 세트 버튼의 자리</summary>
        public Rect SlotButtonRect(int index)
        {
            return new Rect(40f, RunePanelTop + 95f + index * 88f, DesignWidth - 80f, 78f);
        }

        /// <summary>룬 버리기 버튼의 자리</summary>
        public Rect DiscardButtonRect()
        {
            int slots = State != null ? State.Slots.Length : 3;
            return new Rect(40f, RunePanelTop + 95f + slots * 88f, DesignWidth - 80f, 60f);
        }

        /// <summary>다시 시작 버튼의 자리</summary>
        public Rect RestartButtonRect()
        {
            return new Rect(40f, ResultPanelTop + 220f, DesignWidth - 80f, 70f);
        }

        /// <summary>배치 좌표의 한 점을 눌렀을 때의 처리. 버튼을 눌렀으면 true</summary>
        public bool PressAt(Vector2 designPoint)
        {
            if (State == null) return false;
            if (State.Phase == RunPhase.ChoosingRune && State.PendingRunes.Count > 0)
            {
                for (int i = 0; i < State.Slots.Length; i++)
                {
                    if (!SlotButtonRect(i).Contains(designPoint)) continue;
                    ChooseRune(i);
                    return true;
                }
                if (DiscardButtonRect().Contains(designPoint)) { ChooseRune(TickInput.Discard); return true; }
            }
            else if (State.Phase == RunPhase.Dead && RestartButtonRect().Contains(designPoint))
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
            if (State == null) return;

            Pointer pointer = Pointer.current;
            if (pointer != null && pointer.press.wasPressedThisFrame)
            {
                Vector2 screen = pointer.position.ReadValue();
                PressAt(new Vector2(screen.x / DesignScale, (Screen.height - screen.y) / DesignScale));
            }

            Keyboard kb = Keyboard.current;
            if (kb == null) return;
            if (State.Phase == RunPhase.ChoosingRune)
            {
                // 에디터에서 편하게 쓰는 키. 숫자는 세트 번호, X 는 버리기
                if (kb.digit1Key.wasPressedThisFrame && State.Slots.Length > 0) ChooseRune(0);
                if (kb.digit2Key.wasPressedThisFrame && State.Slots.Length > 1) ChooseRune(1);
                if (kb.digit3Key.wasPressedThisFrame && State.Slots.Length > 2) ChooseRune(2);
                if (kb.xKey.wasPressedThisFrame) ChooseRune(TickInput.Discard);
            }
            else if (State.Phase == RunPhase.Dead && (kb.rKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame))
            {
                pendingRestart = true;
            }
        }

        string SlotText(SpellSlot slot)
        {
            if (slot.DeliveryId == null && slot.SubstanceId == null) return "비어 있음";
            string delivery = slot.DeliveryId == null ? "방식 없음" : data.Delivery(slot.DeliveryId).Name;
            string substance = slot.SubstanceId == null ? "물질 없음" : data.Substance(slot.SubstanceId).Name;
            return delivery + " + " + substance;
        }

        string RuneText(string runeId)
        {
            return data.IsDelivery(runeId)
                ? data.Delivery(runeId).Name + " (방식)"
                : data.Substance(runeId).Name + " (물질)";
        }

        void OnGUI()
        {
            float scale = DesignScale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float w = DesignWidth;
            float h = Screen.height / scale;

            if (label == null)
            {
                label = new GUIStyle(GUI.skin.label) { fontSize = 20, wordWrap = true };
                label.normal.textColor = Color.white;
                title = new GUIStyle(label) { fontSize = 28, alignment = TextAnchor.MiddleCenter };
                button = new GUIStyle(GUI.skin.button) { fontSize = 22, wordWrap = true };
            }

            if (!string.IsNullOrEmpty(ErrorText))
            {
                Box(new Rect(20, h / 2 - 200, w - 40, 400));
                GUI.Label(new Rect(40, h / 2 - 180, w - 80, 360), "데이터를 읽지 못했습니다\n\n" + ErrorText, label);
                return;
            }
            if (State == null || data == null) return;

            float hpMax = data.Balance("player_hp");
            GUI.color = new Color(0f, 0f, 0f, 0.5f);
            GUI.DrawTexture(new Rect(16, 40, w - 32, 22), Texture2D.whiteTexture);
            GUI.color = new Color(0.9f, 0.25f, 0.3f);
            GUI.DrawTexture(new Rect(16, 40, (w - 32) * Mathf.Clamp01(State.Player.Hp / hpMax), 22), Texture2D.whiteTexture);
            GUI.color = Color.white;

            int seconds = (int)State.Seconds(data);
            GUI.Label(new Rect(16, 66, w - 32, 28),
                string.Format("{0:00}:{1:00}   처치 {2}   반응 {3}", seconds / 60, seconds % 60, State.Kills, State.ReactionCount), label);
            if (lastReaction != null && Time.unscaledTime - lastReactionTime < ReactionShowSeconds)
                GUI.Label(new Rect(0, 110, w, 40), lastReaction + "!", title);

            Box(new Rect(0, h - 110, w, 110));
            for (int i = 0; i < State.Slots.Length; i++)
                GUI.Label(new Rect(16, h - 104 + i * 32, w - 32, 30), (i + 1) + "번  " + SlotText(State.Slots[i]), label);

            if (State.Phase == RunPhase.ChoosingRune && State.PendingRunes.Count > 0) DrawRuneChoice(w, h);
            else if (State.Phase == RunPhase.Dead) DrawResult(w, h, seconds);
            else if (Paused) GUI.Label(new Rect(0, h / 2 - 30, w, 60), "일시정지", title);
        }

        void DrawRuneChoice(float w, float h)
        {
            float top = RunePanelTop;
            Box(new Rect(20, top, w - 40, 460));
            GUI.Label(new Rect(20, top + 10, w - 40, 50), "새 룬: " + RuneText(State.PendingRunes[0]), title);
            if (State.PendingRunes.Count > 1)
                GUI.Label(new Rect(40, top + 60, w - 80, 28), "남은 룬 " + (State.PendingRunes.Count - 1) + "개", label);
            // 버튼은 그림만 그린다. 누름 판정은 PressAt 이 같은 자리로 한다
            for (int i = 0; i < State.Slots.Length; i++)
                GUI.Box(SlotButtonRect(i), (i + 1) + "번에 끼우기\n지금: " + SlotText(State.Slots[i]), button);
            GUI.Box(DiscardButtonRect(), "버리기", button);
        }

        void DrawResult(float w, float h, int seconds)
        {
            float top = ResultPanelTop;
            Box(new Rect(20, top, w - 40, 320));
            GUI.Label(new Rect(20, top + 10, w - 40, 60), "쓰러졌습니다", title);
            GUI.Label(new Rect(40, top + 80, w - 80, 120),
                string.Format("버틴 시간 {0:00}:{1:00}\n처치 {2}\n반응 {3}", seconds / 60, seconds % 60, State.Kills, State.ReactionCount), label);
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
