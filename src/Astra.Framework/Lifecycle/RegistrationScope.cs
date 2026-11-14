using System;
using System.Threading;

namespace Astra.Lifecycle;

/// <summary>
/// 一次性注册作用域：Dispose 时撤销该来源在 Astra 登记的全部可撤销内容。
/// </summary>
/// <remarks>
/// 作用域不拦截注册调用；它只保存来源并按程序集触发 <see cref="AstraLifecycle.UnregisterSource(RegistrationSource)"/>。
/// 要区分同一程序集里的多个插件，请用不同程序集，或在注册时显式传入更精确的来源（当前 API 以程序集为匹配键）。
/// </remarks>
public sealed class RegistrationScope : IDisposable
{
    private int _disposed;

    internal RegistrationScope(RegistrationSource source)
    {
        Source = source;
    }

    /// <summary>作用域对应的注册来源。</summary>
    public RegistrationSource Source { get; }

    /// <summary>作用域是否已经释放。</summary>
    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    /// <summary>
    /// 立即撤销该来源的全部登记项。重复调用不会重复撤销。
    /// </summary>
    /// <returns>本次实际撤销的登记项数量。</returns>
    public int Unregister()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return 0;

        return AstraLifecycle.UnregisterSource(Source);
    }

    /// <inheritdoc />
    public void Dispose() => Unregister();
}
