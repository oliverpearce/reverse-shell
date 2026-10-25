using System.Runtime.InteropServices;

namespace Game.World
{
    /// <summary>
    /// Web builds: stores save files in the browser straight away (Plugins/WebGL/WebSync.jslib), so Save, then a page
    /// reload, then Continue works. Does nothing anywhere else.
    /// </summary>
    public static class WebSync
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        static extern void SyncFiles();
#endif

        public static void Flush()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            SyncFiles();
#endif
        }
    }
}
