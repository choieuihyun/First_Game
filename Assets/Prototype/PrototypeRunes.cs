// 시제품 전용 코드. 재미 확인이 끝나면 통째로 지운다
using UnityEngine;

namespace Prototype
{
    /// <summary>룬이 들어가는 칸 종류</summary>
    public enum RuneSlot { Shape, Element, Modifier }

    /// <summary>시제품에서 쓰는 룬 목록. None 은 빈 칸</summary>
    public enum RuneId { None, Bolt, Ring, Beam, Fire, Ice, Lightning, Poison, Multi, Split, Homing, Big }

    /// <summary>룬의 칸, 이름, 색 정보를 모아 둔 곳</summary>
    public static class Runes
    {
        /// <summary>룬이 어느 칸에 들어가는지 돌려준다</summary>
        public static RuneSlot SlotOf(RuneId id)
        {
            switch (id)
            {
                case RuneId.Bolt:
                case RuneId.Ring:
                case RuneId.Beam:
                    return RuneSlot.Shape;
                case RuneId.Fire:
                case RuneId.Ice:
                case RuneId.Lightning:
                case RuneId.Poison:
                    return RuneSlot.Element;
                default:
                    return RuneSlot.Modifier;
            }
        }

        /// <summary>화면에 보여 줄 룬 이름</summary>
        public static string NameOf(RuneId id) => id switch
        {
            RuneId.None => "없음",
            RuneId.Bolt => "탄환",
            RuneId.Ring => "고리",
            RuneId.Beam => "광선",
            RuneId.Fire => "불",
            RuneId.Ice => "얼음",
            RuneId.Lightning => "번개",
            RuneId.Poison => "독",
            RuneId.Multi => "다중",
            RuneId.Split => "분열",
            RuneId.Homing => "추적",
            RuneId.Big => "거대",
            _ => id.ToString()
        };

        /// <summary>칸 이름</summary>
        public static string SlotName(RuneSlot slot) => slot switch
        {
            RuneSlot.Shape => "모양",
            RuneSlot.Element => "원소",
            _ => "변형"
        };

        /// <summary>룬 색. 원소는 마법과 적 저항 표시에도 같은 색을 쓴다</summary>
        public static Color ColorOf(RuneId id) => id switch
        {
            RuneId.Fire => new Color(1f, 0.45f, 0.15f),
            RuneId.Ice => new Color(0.55f, 0.85f, 1f),
            RuneId.Lightning => new Color(1f, 0.95f, 0.3f),
            RuneId.Poison => new Color(0.45f, 1f, 0.35f),
            RuneId.None => Color.white,
            _ => SlotOf(id) == RuneSlot.Shape ? new Color(0.8f, 0.7f, 1f) : new Color(0.6f, 1f, 0.85f)
        };
    }
}
