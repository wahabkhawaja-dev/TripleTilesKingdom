using System;
using System.Collections.Generic;
using UnityEngine;

namespace Presentation.Pooling
{
    /// <summary>
    /// Stack-backed pool for any UnityEngine.Component type. No allocations after
    /// Prewarm — Get/Release only push/pop an existing Stack&lt;T&gt; and toggle
    /// GameObject.SetActive. Instances implementing IPoolable receive
    /// OnSpawned/OnDespawned callbacks; everything else works via SetActive alone.
    /// </summary>
    public sealed class GenericObjectPool<T> : IObjectPool<T> where T : Component
    {
        private readonly Stack<T> _inactive;
        private readonly HashSet<T> _active;
        private readonly Func<T> _factory;
        private readonly Transform _inactiveParent;

        public int CountInactive => _inactive.Count;
        public int CountActive => _active.Count;

        public GenericObjectPool(Func<T> factory, Transform inactiveParent, int initialCapacity = 16)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _inactiveParent = inactiveParent;
            _inactive = new Stack<T>(initialCapacity);
            _active = new HashSet<T>(initialCapacity);
        }

        public void Prewarm(int count)
        {
            for (var i = 0; i < count; i++)
            {
                var instance = _factory();
                instance.transform.SetParent(_inactiveParent, false);
                instance.gameObject.SetActive(false);
                _inactive.Push(instance);
            }
        }

        public T Get()
        {
            var instance = _inactive.Count > 0 ? _inactive.Pop() : _factory();
            _active.Add(instance);
            instance.gameObject.SetActive(true);

            if (instance is IPoolable poolable)
            {
                poolable.OnSpawned();
            }

            return instance;
        }

        public void Release(T item)
        {
            if (item == null || !_active.Remove(item))
            {
                // Not null-safe by accident: releasing something this pool never
                // handed out (or double-releasing) is silently ignored rather than
                // throwing, since it can legitimately happen during teardown races
                // (e.g. a match animation completing after the level already ended).
                return;
            }

            if (item is IPoolable poolable)
            {
                poolable.OnDespawned();
            }

            item.transform.SetParent(_inactiveParent, false);
            item.gameObject.SetActive(false);
            _inactive.Push(item);
        }
    }
}
