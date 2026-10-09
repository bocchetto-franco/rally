using UnityEditor;
public static class RallyMenuPagesCheck
{
    public static void Run() => RallyMenuPagesVerification.Run();
    public static string Status() => "playing=" + EditorApplication.isPlaying + ", compiling=" + EditorApplication.isCompiling + ", active=" + SessionState.GetBool("Rally.MenuPagesTest.Active", false) + ", scene=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name + ", canvas=" + (UnityEngine.Object.FindAnyObjectByType<RallyMenuController>() != null);
}
