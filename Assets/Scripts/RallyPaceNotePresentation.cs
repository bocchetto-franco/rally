using UnityEngine;
/// <summary>Shared entry animation and speed intensity for rally hazard cards.</summary>
public static class RallyPaceNotePresentation
{
    public static float Scale(float elapsed,float duration)
    {
        float p=Mathf.Clamp01(elapsed/Mathf.Max(.1f,duration));
        return p<.7f?Mathf.Lerp(.84f,1.04f,Mathf.SmoothStep(0,1,p/.7f)):Mathf.Lerp(1.04f,1,Mathf.SmoothStep(0,1,(p-.7f)/.3f));
    }
    public static float Opacity(float speed,float target,float excess) => Mathf.Lerp(.78f,1f,Mathf.SmoothStep(0,1,Mathf.Clamp01((speed-target)/Mathf.Max(1,excess))));
}
