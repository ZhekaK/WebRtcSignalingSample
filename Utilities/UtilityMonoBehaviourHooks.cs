using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public class UtilityMonoBehaviourHooks
{
    /// <summary>
    /// Action invokes every update
    /// </summary>
    public static event Action OnUpdate;
    /// <summary>
    /// Action invokes every fixed update
    /// </summary>
    public static event Action OnFixedUpdate;
    /// <summary>
    /// Action invokes on quit
    /// </summary>
    public static event Action OnApplicationQuitting;

    private static HookComponent _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureInstance()
    {
        if (_instance == null)
        {
            GameObject go = new GameObject("MonoBehaviourHookManager");
            _instance = go.AddComponent<HookComponent>();
            go.hideFlags = HideFlags.HideInHierarchy;
            GameObject.DontDestroyOnLoad(go);
        }
    }

    private class HookComponent : MonoBehaviour
    {
        private void Update() => OnUpdate?.Invoke();
        private void FixedUpdate() => OnFixedUpdate?.Invoke();
        private void OnApplicationQuit() => OnApplicationQuitting?.Invoke();

        public Coroutine ExecuteCoroutine(IEnumerator routine) => StartCoroutine(routine);
        public void StopExecutingCoroutine(Coroutine routine) => StopCoroutine(routine);
        public void CustomDestroy(UnityEngine.Object obj) => Destroy(obj);
        public void CustomInstantiate(GameObject obj, Vector3 pos, Quaternion rot, Transform parent) => Instantiate(obj, pos,rot, parent);
    }

    /// <summary>
    /// Start coroutine without monobehaviour
    /// </summary>
    /// <param name="routine"></param>
    /// <returns></returns>
    public static Coroutine StartCoroutine(IEnumerator routine)
    {
        EnsureInstance();
        return _instance.ExecuteCoroutine(routine);
    }

    /// <summary>
    /// Stop coroutine without monobehaviour
    /// </summary>
    /// <param name="routine"></param>
    public static void StopCoroutine(Coroutine routine)
    {
        if (_instance != null && routine != null)
            _instance.StopExecutingCoroutine(routine);
    }

    public static void Destroy(UnityEngine.Object obj)
    {
        EnsureInstance();
        _instance.CustomDestroy(obj);
    }

    public static void Instantiate(GameObject obj, Vector3 pos, Quaternion rot, Transform parent)
    {
        EnsureInstance();
        _instance.CustomInstantiate(obj, pos, rot, parent);
    }
}
