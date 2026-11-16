using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.View
{
    /// <summary>터치와 키보드에서 이동 방향을 읽는다. 화면 아무 곳이나 누르고 끌면 그 방향으로 움직인다</summary>
    public sealed class InputReader
    {
        // 이만큼 끌면 최대 속도가 된다 (픽셀)
        const float FullDragPixels = 80f;
        // 이보다 작은 끌기는 손떨림으로 보고 무시한다. 앞 방향이 엉뚱하게 뒤집히지 않게 하기 위한 것
        const float Deadzone = 0.2f;

        bool dragging;
        Vector2 dragStart;

        /// <summary>끌기 상태를 버린다. 판이 멈춘 화면에서 누른 것이 이동으로 이어지지 않게 한다</summary>
        public void Reset()
        {
            dragging = false;
        }

        /// <summary>끌기 값을 다듬는다. 아주 작은 값은 0 으로, 1 을 넘는 값은 1 로</summary>
        public static Vector2 ApplyDeadzone(Vector2 raw)
        {
            float magnitude = raw.magnitude;
            if (magnitude < Deadzone) return Vector2.zero;
            return magnitude > 1f ? raw / magnitude : raw;
        }

        /// <summary>이동 방향을 돌려준다. 길이는 1 이하</summary>
        public Vector2 ReadMove()
        {
            Vector2 move = Vector2.zero;

            Pointer pointer = Pointer.current;
            if (pointer != null && pointer.press.isPressed)
            {
                Vector2 pos = pointer.position.ReadValue();
                if (!dragging) { dragging = true; dragStart = pos; }
                move = ApplyDeadzone((pos - dragStart) / FullDragPixels);
            }
            else dragging = false;

            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move.y -= 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move.x -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move.x += 1f;
            }

            return move.sqrMagnitude > 1f ? move.normalized : move;
        }
    }
}
