using LabApi.Features.Wrappers;

using UnityEngine;

namespace Astra.Effects;
    public abstract class CustomEffect
    {
        public Player Owner { get; }
        public byte Intensity { get; private set; }
        public float Duration { get; private set; }
        public float TimeLeft { get; private set; }
        public virtual byte MaxIntensity => byte.MaxValue;
        public virtual bool AllowEnabling => true;

        /// <summary>是否允许同类效果叠加层数；默认 <see langword="false"/>，保持原有行为。</summary>
        public virtual bool CanStack => false;

        /// <summary>当前叠加层数；未叠加时为 1，禁用时重置为 1。</summary>
        public int StackCount { get; private set; } = 1;
        public bool IsEnabled => Intensity > 0;
        public CustomEffect(Player player)
        {
            Owner = player;
            OnInitialize();
        }
        public void SetState(byte intensity, float duration = 0f, bool addDuration = false)
        {
            ForceIntensity(intensity);
            ChangeDuration(duration, addDuration);
        }
        public void ForceIntensity(byte value)
        {
            if (Intensity == value)
                return;
            byte oldIntensity = Intensity;
            bool wasEnabled = Intensity > 0;
            Intensity = (byte)Mathf.Min(value, MaxIntensity);
            bool nowEnabled = Intensity > 0;

            if (wasEnabled != nowEnabled)
            {
                if (nowEnabled)
                    Enabled();
                else
                    Disabled();
            }
            IntensityChanged(oldIntensity, Intensity);
        }
        public void ChangeDuration(float duration, bool addDuration)
        {
            if (addDuration && duration > 0f)
            {
                Duration += duration;
                TimeLeft += duration;
            }
            else
            {
                Duration = Mathf.Max(0f, duration);
                TimeLeft = Duration;
            }
            if (Duration > 0f && TimeLeft <= 0f)
                DisableEffect();
        }
        public void Update()
        {
            if (IsEnabled)
            {
                if (Duration > 0f)
                {
                    TimeLeft -= Time.deltaTime;
                    if (TimeLeft <= 0f)
                        DisableEffect();
                }
                OnEffectUpdate();
            }
        }
        public void DisableEffect()
        {
            Duration = 0f;
            StackCount = 1;
            ForceIntensity(0);
        }

        /// <summary>叠加一层：层数 +1，强度按层数放大（不超过上限），时长累加。</summary>
        /// <param name="duration">本次叠加的时长；<c>0</c> 表示沿用无时长语义。</param>
        internal void Stack(float duration)
        {
            StackCount++;
            var scaled = (byte)Mathf.Min((int)Intensity * StackCount, (int)MaxIntensity);
            SetState(scaled, duration, addDuration: true);
        }
        public virtual void OnInitialize() { }
        public virtual void OnDestroy() { }
        public virtual void Enabled() { }
        public virtual void Disabled() { }
        public virtual void OnEffectUpdate() { }
        public virtual void IntensityChanged(byte oldIntensity, byte newIntensity) { }
        public virtual void OnOwnerDeath() => DisableEffect();
        public virtual void OnOwnerRoleChanged() => DisableEffect();
    }

