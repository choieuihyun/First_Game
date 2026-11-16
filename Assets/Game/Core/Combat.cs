namespace Game.Core
{
    /// <summary>피해 계산을 한곳에 모은다. 모든 피해는 여기를 거쳐야 약화 상태가 빠짐없이 적용된다</summary>
    public static class Combat
    {
        /// <summary>적에게 피해를 준다. 약화 상태면 배율만큼 더 들어간다</summary>
        public static void Damage(EnemyState enemy, float amount)
        {
            if (amount <= 0f) return;
            if (enemy.VulnTime > 0f) amount *= enemy.VulnMul;
            enemy.Hp -= amount;
        }
    }
}
