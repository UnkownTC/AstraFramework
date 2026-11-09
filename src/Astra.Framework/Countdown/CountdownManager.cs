using System;
using System.Collections.Generic;

using Astra.Loader;

using UnityEngine;

using Logger = LabApi.Features.Console.Logger;
using Astra;
using Astra.Attributes;
using Astra.Enums;
using Astra.Extensions;
using Astra.Roles;
using Astra.Teams;
using Astra.Waves;
using Astra.Items;
using Astra.Effects;
using Astra.Abilities;
using Astra.HumeShield;
using Astra.Patching;
using Astra.Hints;
using Astra.Audio;
using Astra.Cancellation;
using Astra.ServerSpecificSettings;

namespace Astra.Countdown;
    /// <summary>
    /// 计时器管理单例。
    /// 内部使用二叉最小堆（按触发时间排序）实现优先级队列，
    /// 使 AddTimer 为 O(log n)、Update 处理到期计时器为 O(k log n)，
    /// 移除计时器通过惰性删除（<see cref="Timer.Cancelled"/> 标记）降为 O(1)。
    /// <para>
    /// <c>TickAction</c> 的生命周期**独立于「到期」**：计时器在堆中等待触发期间，
    /// 由 <see cref="ProcessTicks"/> 每 <see cref="TickInterval"/> 秒回调一次；
    /// 到期时执行 <c>Action</c>，再按 <c>Repeat</c> 决定是否重新入队。
    /// 因此计时器必须同时登记在最小堆（排序）与活动表（逐帧 tick）中。
    /// </para>
    /// </summary>
    public class CountdownManager : MonoSingleton<CountdownManager>
    {
        /// <summary>TickAction 的调用间隔（秒）。</summary>
        public const float TickInterval = 1f;

        /// <summary>
        /// 计时器。
        /// 必须是引用类型：同一实例被最小堆与活动表共同持有，
        /// 且 <see cref="Time"/> / <see cref="NextTickTime"/> 需要就地更新。
        /// </summary>
        private sealed class Timer
        {
            /// <summary>下一次触发时间（堆排序键）。</summary>
            public float Time;

            /// <summary>触发延迟；重复入队时沿用。</summary>
            public readonly float Delay;

            /// <summary>进程内唯一标识；重复入队时保持不变，便于外部取消。</summary>
            public readonly int Uuid;

            /// <summary>到期回调。</summary>
            public readonly Action Action;

            /// <summary>等待触发期间每秒回调一次；可为 null。</summary>
            public readonly Action TickAction;

            /// <summary>返回 true 则到期后按原 delay 重新入队。</summary>
            public readonly Func<bool> Repeat;

            /// <summary>下一次 TickAction 的时间。</summary>
            public float NextTickTime;

            /// <summary>是否已被移除；堆中残留的节点在弹出时按此标记丢弃（惰性删除）。</summary>
            public bool Cancelled;

            public Timer(float time, float delay, int uuid, Action action, Action tickAction, Func<bool> repeat)
            {
                Time = time;
                Delay = delay;
                Uuid = uuid;
                Action = action;
                TickAction = tickAction;
                Repeat = repeat;
                // 入队时间 = time - delay，故首次 tick 落在 入队时间 + TickInterval。
                NextTickTime = time - delay + TickInterval;
            }
        }

        /// <summary>二叉最小堆，堆顶为触发时间最早的计时器。</summary>
        private readonly List<Timer> _heap = new(16);

        /// <summary>尚未到期（含重复循环中）的计时器索引：Uuid → Timer。为 null 时快速跳过。</summary>
        private readonly Dictionary<int, Timer> _active = new();

        /// <summary>tick 遍历缓冲：回调内部可能增删计时器，不能直接遍历 <see cref="_active"/>。</summary>
        private readonly List<Timer> _tickBuffer = new(16);

        /// <summary>自增 Uuid 计数器，保证进程内唯一。</summary>
        private int _nextUuid;

        /// <summary>
        /// 初始化
        /// </summary>
        public void Init()
        {
            ClearTimer();
        }

        private void Update()
        {
            var now = Time.unscaledTime;

            // 1) 先派发「等待触发期间」的每秒 tick。
            //    必须早于到期派发：到期回调通常是「结束效果」，而 tick 是「维持效果」。
            if (_active.Count > 0)
                ProcessTicks(now);

            // 2) 再处理到期。
            if (_heap.Count == 0)
                return;

            while (TryPopDue(now, out Timer timer))
            {
                var repeat = timer.Repeat?.Invoke() ?? false;

                try
                { timer.Action(); }
                catch (Exception e)
                {
                    Logger.Error($"Timer:{timer.Uuid} Target:{timer.Action.Target}\nException:{e}");
                }

                if (repeat)
                {
                    Reschedule(timer, now);
                    Push(timer);
                }
                else
                {
                    _active.Remove(timer.Uuid);
                }
            }
        }

        /// <summary>
        /// 向所有「尚未到期」的计时器派发每秒一次的 <see cref="Timer.TickAction"/>。
        /// 每个计时器持有自己的 <see cref="Timer.NextTickTime"/>，互不节流。
        /// </summary>
        private void ProcessTicks(float now)
        {
            // 快照：TickAction 内部可能 AddTimer / RemoveTimer，直接遍历 _active 会抛 InvalidOperationException。
            _tickBuffer.Clear();
            foreach (var pair in _active)
                _tickBuffer.Add(pair.Value);

            foreach (var timer in _tickBuffer)
            {
                if (timer.Cancelled || timer.TickAction is null)
                    continue;
                if (now < timer.NextTickTime)
                    continue;

                timer.NextTickTime = now + TickInterval;

                try
                { timer.TickAction(); }
                catch (Exception e)
                {
                    Logger.Error($"Timer:{timer.Uuid} Target:{timer.TickAction.Target}\nException:{e}");
                }
            }
        }

        /// <summary>
        /// 添加计时器
        /// </summary>
        /// <param name="delay">延迟秒数</param>
        /// <param name="action">到期时执行的回调</param>
        /// <param name="tickAction">倒计时期间每秒执行一次的 tick 回调，可为空</param>
        /// <param name="repeat">返回 true 则到期后按原 delay 重新入队</param>
        /// <returns>计时器唯一标识</returns>
        public int AddTimer(float delay, Action action, Action tickAction = null, Func<bool> repeat = null)
        {
            if (delay < 0)
                throw new ArgumentException("delay不能为负数", nameof(delay));
            if (action == null)
                throw new ArgumentException("action 不能为null", nameof(action));

            var timer = new Timer(Time.unscaledTime + delay, delay, _nextUuid++, action, tickAction, repeat);
            _active[timer.Uuid] = timer;
            Push(timer);
            return timer.Uuid;
        }

        /// <summary>
        /// 移除计时器（惰性删除，O(1)）
        /// </summary>
        /// <param name="uuid">计时器唯一标识</param>
        public void RemoveTimer(int uuid)
        {
            if (!_active.TryGetValue(uuid, out var timer))
                return;
            timer.Cancelled = true;   // 堆中的残留节点在弹出时被丢弃
            _active.Remove(uuid);
        }

        /// <summary>
        /// 清除计时器
        /// </summary>
        public void ClearTimer()
        {
            // 堆被整体清空，无需逐个标记 Cancelled。
            _heap.Clear();
            _active.Clear();
        }

        /// <summary>重复计时器重新入队：重置触发时间与下一次 tick 时间。</summary>
        private static void Reschedule(Timer timer, float now)
        {
            timer.Time = now + timer.Delay;
            timer.NextTickTime = now + TickInterval;
        }

        /// <summary>
        /// 弹出堆顶已到期（且未取消）的计时器；无到期计时器时返回 false。
        /// </summary>
        private bool TryPopDue(float now, out Timer timer)
        {
            timer = null!;
            while (_heap.Count > 0)
            {
                // 跳过堆顶已取消的计时器
                if (_heap[0].Cancelled)
                {
                    Pop();
                    continue;
                }
                if (_heap[0].Time > now)
                    return false;
                timer = _heap[0];
                Pop();
                return true;
            }
            return false;
        }

        /// <summary>
        /// 向堆中插入计时器并上浮（O(log n)）
        /// </summary>
        private void Push(Timer timer)
        {
            _heap.Add(timer);
            var i = _heap.Count - 1;
            while (i > 0)
            {
                var parent = (i - 1) >> 1;
                if (_heap[parent].Time <= _heap[i].Time)
                    break;
                Swap(i, parent);
                i = parent;
            }
        }

        /// <summary>
        /// 移除堆顶并下沉（O(log n)）
        /// </summary>
        private void Pop()
        {
            var last = _heap.Count - 1;
            _heap[0] = _heap[last];
            _heap.RemoveAt(last);
            if (_heap.Count == 0)
                return;

            var i = 0;
            while (true)
            {
                var left = (i << 1) + 1;
                var right = left + 1;
                var smallest = i;
                if (left < _heap.Count && _heap[left].Time < _heap[smallest].Time)
                    smallest = left;
                if (right < _heap.Count && _heap[right].Time < _heap[smallest].Time)
                    smallest = right;
                if (smallest == i)
                    break;
                Swap(i, smallest);
                i = smallest;
            }
        }

        private void Swap(int a, int b)
        {
            var tmp = _heap[a];
            _heap[a] = _heap[b];
            _heap[b] = tmp;
        }
    }

