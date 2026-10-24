using UnityEngine;

namespace Combat
{
    /// <summary>
    /// How the six elements are named and colored. Rules (the advantage wheel) live in <see cref="AfflatusChart"/>;
    /// this is presentation only. The set was picked to match the free Super Pixel Effects Gigapack animations.
    /// </summary>
    public static class AfflatusInfo
    {
        public static string Display(Afflatus afflatus)
        {
            if (afflatus == Afflatus.None) return "Physical";
            if (afflatus == Afflatus.Volt) return "Electric";
            return afflatus.ToString();
        }

        /// <summary>The element's accent color (glows, labels).</summary>
        public static Color Color(Afflatus afflatus)
        {
            switch (afflatus)
            {
                case Afflatus.Volt: return new Color(0.45f, 0.78f, 1f);     // electric blue
                case Afflatus.Fire: return new Color(1f, 0.55f, 0.25f);     // orange
                case Afflatus.Toxin: return new Color(0.78f, 0.95f, 0.2f);  // acid chartreuse
                case Afflatus.Earth: return new Color(0.5f, 0.92f, 0.45f);  // moss green (Heal Bot)
                case Afflatus.Light: return new Color(1f, 0.92f, 0.6f);     // warm gold-white
                case Afflatus.Void: return new Color(0.78f, 0.45f, 1f);     // violet (Lucy)
                case Afflatus.None: return new Color(0.78f, 0.76f, 0.72f);  // Physical: plain steel
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
