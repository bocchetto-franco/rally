using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
public static class RallyGameViewSize
{
    const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
    public static object Inspect()
    {
        var assembly=typeof(EditorWindow).Assembly;
        return new[]{"UnityEditor.GameViewSizes","UnityEditor.GameViewSizeGroup","UnityEditor.GameViewSize","UnityEditor.GameView"}
            .Select(name=> {var type=assembly.GetType(name);return new {name,methods=type.GetMethods(Flags).Where(m=>m.Name.Contains("Size")||m.Name.Contains("Group")).Select(m=>m.ToString()).ToArray(),properties=type.GetProperties(Flags).Where(p=>p.Name.Contains("Size")||p.Name=="instance"||p.Name=="width"||p.Name=="height").Select(p=>p.ToString()).ToArray(),constructors=type.GetConstructors(Flags).Select(c=>c.ToString()).ToArray()};}).ToArray();
    }
}
