namespace StopFarmingItsEnough
{
    /// <summary>纯逻辑施密特触发器；不依赖游戏，便于单独验证边界行为。</summary>
    public static class Hysteresis
    {
        /// <summary>严格大于上限暂停，严格小于下限恢复；区间内及等于边界时保持原状态。</summary>
        public static bool Next(bool paused, int stock, int lower, int upper)
        {
            if (stock > upper) return true;
            if (stock < lower) return false;
            return paused;
        }

        /// <summary>检查阈值有效性；下限允许为零，此时暂停后不能靠非负库存自动恢复。</summary>
        public static bool Valid(int lower, int upper)
        {
            return lower >= 0 && upper > lower;
        }
    }
}

