using LabApi.Features.Wrappers;

using UnityEngine;

namespace Astra.Abstractions
{
    public abstract class CustomEffect
    {
        public Player Owner { get; }
        public byte Intensity { get; private set; }
        public float Duration { get; private set; }
        public float TimeLeft { get; private set; }
        public virtual byte MaxIntensity => byte.MaxValue;
        public virtual bool AllowEnabling => true;
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
            ForceIntensity(0);
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
}
