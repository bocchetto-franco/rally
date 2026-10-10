using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
public static class RallyFourUXStop
{
    public static object Run()
    {
        var callbacks=EditorApplication.update.GetInvocationList().Where(d=>d.Method.DeclaringType.Name.Contains("RallyFourUXPlay")).ToArray();
        var descriptions=callbacks.Select(d=>d.Method.DeclaringType.FullName+" / "+d.Method.DeclaringType.Assembly.FullName).ToArray();
        foreach(var callback in callbacks)
        {
            EditorApplication.update-=(EditorApplication.CallbackFunction)callback;
            callback.Method.DeclaringType.GetMethod("Restore",BindingFlags.NonPublic|BindingFlags.Static)?.Invoke(null,null);
        }
        return descriptions;
    }
}
