using UnityEngine;

namespace Combat
{
    /// <summary>
    /// A painted battle background, generated at runtime as pixel art from a tile atlas (the same one the
    /// overworld uses), so each area's fights match its look. Assign one to BattleSetup.backdrop.
    /// </summary>
    [CreateAssetMenu(menuName = "Combat/Battle Backdrop", fileName = "Backdrop_New")]
    public class BattleBackdropDefinition : ScriptableObject
    {
        public string displayName = "Rainline Station";

        [Tooltip("Tile atlas to paint building facades and streets from (e.g. CyberCity city.png). Must be Read/Write enabled.")]
        public Texture2D tileAtlas;
        [Tooltip("Size of one atlas cell in texture pixels.")]
        public int atlasCell = 64;
        [Tooltip("How many texture pixels make one art pixel (CyberCity is 16px art drawn at 4x).")]
        public int atlasPixelScale = 4;

        [Tooltip("Same seed = same skyline.")]
        public int seed = 1999;

        [Header("Sky")]
        public Color skyTop = new Color(0.03f, 0.02f, 0.07f);
        public Color skyHorizon = new Color(0.26f, 0.07f, 0.26f);

        [Header("Neon")]
        public Color[] neon =
        {
            new Color(1f, 0.18f, 0.55f), new Color(0.18f, 0.95f, 1f), new Color(0.7f, 1f, 0.23f), new Color(1f, 0.6f, 0.24f)
        };

        [Header("Layout")]
        [Tooltip("Where the street meets the buildings, as a fraction of screen height from the top.")]
        [Range(0.2f, 0.7f)] public float streetLine = 0.37f;
        [Tooltip("How dark the building facades are painted (night).")]
        [Range(0.2f, 1f)] public float facadeBrightness = 0.55f;

        [Header("Weather")]
        public bool rain = true;
        [Range(0f, 2f)] public float rainIntensity = 1f;
    }
}
