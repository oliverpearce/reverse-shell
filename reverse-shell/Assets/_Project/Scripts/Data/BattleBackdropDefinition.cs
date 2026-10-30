using UnityEngine;
using UnityEngine.Serialization;

namespace Combat
{
    /// <summary>
    /// A painted battle background, generated at runtime as pixel art from a tile atlas (the same one the
    /// overworld uses), so each area's fights match its look. Assign one to BattleSetup.backdrop.
    /// </summary>
    [CreateAssetMenu(menuName = "Combat/Battle Backdrop", fileName = "Backdrop_New")]
    public class BattleBackdropDefinition : ScriptableObject
    {
        public string displayName = "Mossline Station";

        [Tooltip("Tile atlas to paint building facades and streets from (e.g. Art/Tiles/ruins.png). Must be Read/Write enabled.")]
        public Texture2D tileAtlas;
        [Tooltip("Size of one atlas cell in texture pixels.")]
        public int atlasCell = 64;
        [Tooltip("How many texture pixels make one art pixel (the ruins atlas is 16px art drawn at 4x).")]
        public int atlasPixelScale = 4;

        [Tooltip("Same seed = same skyline.")]
        public int seed = 1999;

        [Header("Sky")]
        public Color skyTop = new Color(0.52f, 0.66f, 0.8f);
        public Color skyHorizon = new Color(0.88f, 0.88f, 0.8f);

        [Header("Foliage")]
        [Tooltip("Leaf colors from shadow to sunlit highlight (trees, vines, grass).")]
        public Color[] foliage =
        {
            new Color(0.14f, 0.27f, 0.16f), new Color(0.21f, 0.42f, 0.2f), new Color(0.33f, 0.57f, 0.25f), new Color(0.55f, 0.75f, 0.34f)
        };

        [Header("Layout")]
        [Tooltip("Where the street meets the buildings, as a fraction of screen height from the top.")]
        [Range(0.2f, 0.7f)] public float streetLine = 0.52f;
        [Tooltip("How bright the building facades are painted.")]
        [Range(0.2f, 1f)] public float facadeBrightness = 0.9f;

        [Header("Weather")]
        [Tooltip("Leaves and pollen drifting across the scene.")]
        [FormerlySerializedAs("rain")] public bool driftingLeaves = true;
        [FormerlySerializedAs("rainIntensity")] [Range(0f, 2f)] public float driftIntensity = 1f;
        [Tooltip("Recolours the drift into flecks of this colour (snow, embers, ash, spores); clear = the green leaves.")]
        public Color driftColor = new Color(0f, 0f, 0f, 0f);
        [Tooltip("1 = the drift falls, -1 = it rises (embers).")]
        public float driftDirection = 1f;
    }
}
