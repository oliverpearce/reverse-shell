using UnityEngine;

namespace Combat
{
    /// <summary>
    /// How the six afflatus values are named and colored in this game. Rules (the advantage wheel) live in
    /// <see cref="AfflatusChart"/>; this is presentation only. Plant reads as "Earth" and Intellect as "Virus".
    /// </summary>
    public static class AfflatusInfo
    {
        public static string Display(Afflatus afflatus)
        {
            switch (afflatus)
            {
                case Afflatus.Plant: return "Earth";
                case Afflatus.Intellect: return "Virus";
                default: return afflatus.ToString();
            }
        }

        /// <summary>The element's accent color (glows, labels).</summary>
        public static Color Color(Afflatus afflatus)
        {
            switch (afflatus)
            {
                case Afflatus.Star: return new Color(1f, 0.86f, 0.5f);
                case Afflatus.Mineral: return new Color(0.62f, 0.78f, 0.95f);
                case Afflatus.Beast: return new Color(0.95f, 0.5f, 0.35f);
                case Afflatus.Plant: return new Color(0.5f, 0.92f, 0.45f);
                case Afflatus.Spirit: return new Color(0.55f, 0.95f, 0.95f);
                case Afflatus.Intellect: return new Color(0.78f, 0.45f, 1f);
            }
            return UnityEngine.Color.white;
        }

        /// <summary>
        /// The glow a character shows. A neutral-glow member (MC) shows its own glow color until it channels an
        /// element; everyone else glows in their element. Pass <paramref name="channeled"/> once channeling exists.
        /// </summary>
        public static Color GlowFor(CharacterDefinition member, Afflatus? channeled = null)
        {
            if (member == null) return UnityEngine.Color.white;
            if (channeled.HasValue) return Color(channeled.Value);
            return member.neutralGlow ? member.glowColor : Color(member.afflatus);
        }

        /// <summary>Element label for menus: "MULTI" for members that can channel several elements.</summary>
        public static string LabelFor(CharacterDefinition member)
        {
            if (member != null && member.channels != null && member.channels.Count > 1) return "Multi";
            return member != null ? Display(member.afflatus) : "";
        }
    }
}
