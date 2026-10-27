using UnityEditor;
using UnityEditor.SceneManagement;

namespace Game.EditorTools
{
    /// <summary>
    /// Pressing Play always starts the game from the title screen: Unity boots the overworld scene first, whatever scene
    /// is open (Battle included), and the title shows because WorldSession.TitleDone starts false every Play. Turn it off
    /// with Reverse Shell / Play From Title (remembered per machine) to play the open scene directly.
    /// </summary>
    [InitializeOnLoad]
    static class PlayFromTitle
    {
        const string ScenePath = "Assets/_Project/Scenes/Overworld_Mossline.unity";
        const string MenuPath = "Reverse Shell/Play From Title";
        const string PrefKey = "ReverseShell.PlayFromTitle";

        static PlayFromTitle()
        {
            EditorApplication.delayCall += Apply;
            // The Level Designer's Play This Area always starts in the overworld; afterwards, back to the usual.
            EditorApplication.playModeStateChanged += change =>
            {
                if (change == PlayModeStateChange.EnteredEditMode) Apply();
            };
        }

        static bool Enabled
        {
            get { return EditorPrefs.GetBool(PrefKey, true); }
            set { EditorPrefs.SetBool(PrefKey, value); }
        }

        static void Apply()
        {
            EditorSceneManager.playModeStartScene = Enabled ? AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) : null;
        }

        [MenuItem(MenuPath)]
        static void Toggle()
        {
            Enabled = !Enabled;
            Apply();
        }

        [MenuItem(MenuPath, true)]
        static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, Enabled);
            return true;
        }
    }
}
