using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bonkadoo.Infrastructure.Unity
{
    /// <summary>Runs SDK callbacks on Unity's main thread. Must be created from the main thread first.</summary>
    public sealed class MainThread : MonoBehaviour
    {
        private static readonly Queue<Action> Pending = new Queue<Action>();
        private static MainThread _instance;

        public static void EnsureCreated()
        {
            if (_instance != null) return;
            var go = new GameObject("MainThreadDispatcher") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<MainThread>();
        }

        public static void Post(Action action)
        {
            if (action == null) return;
            lock (Pending) Pending.Enqueue(action);
        }

        private void Update()
        {
            while (true)
            {
                Action next;
                lock (Pending)
                {
                    if (Pending.Count == 0) return;
                    next = Pending.Dequeue();
                }
                try { next(); }
                catch (Exception ex) { Debug.LogException(ex); }
            }
        }
    }
}
