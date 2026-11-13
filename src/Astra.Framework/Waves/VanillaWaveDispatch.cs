using System;

namespace Astra.Waves;

/// <summary>
/// 原版波次分发的状态标记：用 <see cref="Begin"/> 包裹原版 RespawnWave 调用，
/// 期间可通过 <see cref="IsDispatching"/> 区分「原版波次」与「自定义波次」。
/// </summary>
/// <remarks>使用可嵌套的计数器而不是布尔值，避免原版波次内部再次分发时提前复位。</remarks>
public static class VanillaWaveDispatch
{
    [ThreadStatic]
    private static int _depth;

    /// <summary>当前线程是否处于原版波次分发过程中。</summary>
    public static bool IsDispatching => _depth > 0;

    /// <summary>进入原版波次分发作用域；可嵌套，<see cref="IDisposable.Dispose"/> 时逐层退出。</summary>
    /// <returns>作用域包装；建议用 <c>using</c> 包裹原版调用。</returns>
    public static IDisposable Begin()
    {
        _depth++;
        return new Scope();
    }

    private sealed class Scope : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            if (_depth > 0)
                _depth--;
        }
    }
}
