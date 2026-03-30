using System;
using System.Collections.Generic;
using UnityEngine;

public static class UtilityMainThreadDispatcher
{
    // Queue to store delegates that need to be executed on the main thread.
    private static readonly Queue<Action> _executionQueue = new Queue<Action>();
    // Object for synchronizing access to the queue from different threads.
    private static readonly object _lock = new object();


    /// <summary> Adds a delegate to the queue for execution on the main thread </summary>
    public static void Enqueue(Action action)
    {
        lock (_lock)
        {
            _executionQueue.Enqueue(action);
        }
    }

    /// <summary> Executes all delegates from the queue on the main thread. Must be called on the main thread (e.g., in Update or LateUpdate) </summary>
    public static void Execute()
    {
        lock (_lock)
        {
            while (_executionQueue.Count > 0)
            {
                var action = _executionQueue.Dequeue();
                try
                {
                    action?.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"MainThreadDispatcher error: {ex}");
                }
            }
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void MyAwake()
    {
        UtilityMonoBehaviourHooks.OnUpdate += MyUpdate;
        UtilityMonoBehaviourHooks.OnApplicationQuitting += MyQuit;
    }

    private static void MyUpdate()
    {
        Execute();
    }

    private static void MyQuit()
    {
        UtilityMonoBehaviourHooks.OnUpdate -= MyUpdate;
        UtilityMonoBehaviourHooks.OnApplicationQuitting -= MyQuit;
    }
}