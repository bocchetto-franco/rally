using System.Collections.Generic;
using System.Collections;
using UnityEngine;

/// <summary>One idle cleanup for menu landscapes and both vehicle studios.</summary>
public sealed class RallyMenuAssetLifetime : MonoBehaviour
{
    static RallyMenuAssetLifetime instance;
    static bool quitting;
    readonly List<AsyncOperation> pending = new List<AsyncOperation>();
    AsyncOperation cleanup;
    float idleUntil;
    bool requested;
    public static bool IsUnloading => instance != null && instance.cleanup != null && !instance.cleanup.isDone;
    public static bool CleanupPending => instance != null && (instance.requested || IsUnloading);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { instance = null; quitting = false; }

    static RallyMenuAssetLifetime Ensure()
    {
        if(instance == null) {
            instance = new GameObject("Menu asset lifetime - shared idle cleanup").AddComponent<RallyMenuAssetLifetime>();
            DontDestroyOnLoad(instance.gameObject);
        }
        return instance;
    }
    public static void Track(AsyncOperation operation)
    {
        if(operation == null || quitting || !Application.isPlaying) return;
        var owner = Ensure(); owner.pending.Add(operation); RequestCleanup();
    }
    public static void RequestCleanup()
    {
        if(quitting || !Application.isPlaying) return;
        var owner = Ensure(); owner.requested = true; owner.idleUntil = Time.unscaledTime + .75f;
    }
    void OnApplicationQuit() => quitting = true;
    public static IEnumerator PrepareRace()
    {
        if(instance == null) yield break;
        while(instance.pending.Count != 0 || IsUnloading) yield return null;
        // Consume menu cleanup under the loading overlay, never on the first racing frame.
        instance.requested = false;
        instance.cleanup = Resources.UnloadUnusedAssets();
        yield return instance.cleanup;
    }
    void LateUpdate()
    {
        for(int i=pending.Count-1;i>=0;i--) {
            if(!pending[i].isDone) continue;
            pending.RemoveAt(i); idleUntil = Time.unscaledTime + .75f;
        }
        if(!requested || pending.Count != 0 || Time.unscaledTime < idleUntil ||
            IsUnloading || RallyRaceLoading.IsPreparing) return;
        requested = false;
        cleanup = Resources.UnloadUnusedAssets();
    }
}
