namespace Astra.Enums
{
    /// <summary>
    /// 能力触发失败的原因。
    /// </summary>
    public enum AbilityFailReason
    {
        /// <summary>无失败。</summary>
        None,

        /// <summary>玩家条件不满足。</summary>
        ConditionFail,

        /// <summary>冷却未结束。</summary>
        NotReady
    }
}
