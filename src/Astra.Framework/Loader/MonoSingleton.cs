using UnityEngine;
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
using Astra.Countdown;
using Astra.Cancellation;
using Astra.ServerSpecificSettings;

namespace Astra.Loader;
    /// <summary>
    /// Unity 懒加载单例基类：首次访问 <see cref="Instance"/> 时自动创建 GameObject 并 <c>DontDestroyOnLoad</c>，
    /// 保证场景切换时单例不被销毁。
    /// </summary>
    /// <typeparam name="T">单例类型。</typeparam>
    public abstract class MonoSingleton<T> : MonoBehaviour where T : MonoSingleton<T>
    {
        private static T _instance;

        /// <summary>宿主 GameObject。</summary>
        protected GameObject GameObject { get; private set; }

        /// <summary>获取单例实例，不存在时自动创建。</summary>
        public static T Instance
        {
            get
            {
                if (_instance != null)
                    return _instance;
                var obj = new GameObject(typeof(T).Name);
                _instance = obj.AddComponent<T>();
                _instance.GameObject = obj;
                DontDestroyOnLoad(obj);
                return _instance;
            }
        }

        /// <summary>Unity Awake：确保单例唯一，重复创建的实例会被销毁。</summary>
        protected virtual void Awake()
        {
            if (_instance == null)
            {
                _instance = this as T;
                DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        /// <summary>Unity OnDestroy：单例销毁时清空静态引用。</summary>
        protected virtual void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }
    }

