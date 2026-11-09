namespace Astra.Enums;
    /// <summary>
    /// 取消令牌（CancellationToken）的类别。
    /// </summary>
    public enum CancellationTokenCategory
    {
        /// <summary>框架禁用时取消。</summary>
        FrameworkDisabled,

        /// <summary>回合重启时取消。</summary>
        RoundRestarted
    }

