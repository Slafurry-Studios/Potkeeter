using System;
using System.Collections.Generic;
using UnityEngine;

namespace Slafurry.Core.Bridge
{
    [DisallowMultipleComponent]
    public class SingletonEventsBridge : MonoBehaviour
    {
        private readonly Dictionary<Type, ISubBridge> _bridges = new();

        private void Awake()
        {
            // Otomatis mendeteksi semua Sub-Bridge yang dipasang di GameObject ini
            var foundBridges = GetComponents<ISubBridge>();
            foreach (var bridge in foundBridges)
            {
                Type type = bridge.GetType();
                if (!_bridges.ContainsKey(type))
                {
                    _bridges.Add(type, bridge);
                }
            }
        }

        /// <summary>
        /// Mendapatkan instance Sub-Bridge tertentu secara dinamis
        /// </summary>
        public T GetBridge<T>() where T : class, ISubBridge
        {
            Type type = typeof(T);
            if (_bridges.TryGetValue(type, out var bridge))
            {
                return bridge as T;
            }

            T component = GetComponent<T>();
            if (component != null)
            {
                _bridges[type] = component;
                return component;
            }

            Debug.LogWarning($"[SingletonEventsBridge] SubBridge '{type.Name}' tidak ditemukan di GameObject '{gameObject.name}'!");
            return null;
        }
    }
}