using UnityEngine;

namespace Combat
{
    /// <summary>
    /// How the resonances are named and colored. Rules (the advantage wheel) live in <see cref="ResonanceChart"/>;
    /// this is presentation only.
    /// </summary>
    public static class ResonanceInfo
    {
        public static string Display(Resonance resonance) { return resonance.ToString(); }

        /// <summary>Reads a saved resonance name, including the old element names (Volt, Fire, Toxin, Earth, Light,
        /// Void, None) from saves made before bible v0.6.</summary>
        public static bool TryParse(string name, out Resonance resonance)
        {
            switch (name)
            {
                case "Volt": resonance = Resonance.Arc; return true;
                case "Fire": resonance = Resonance.Cinder; return true;
                case "Toxin": case "Earth": resonance = Resonance.Root; return true;
                case "Light": resonance = Resonance.Prism; return true;
                case "Physical": case "Void": case "None": resonance = Resonance.Basic; return true;
            }
            return System.Enum.TryParse(name, out resonance) && System.Enum.IsDefined(typeof(Resonance), resonance);
        }

        /// <summary>The resonance's accent color (glows, labels, packs).</summary>
        public static Color Color(Resonance resonance)
        {
            switch (resonance)
            {
                case Resonance.Arc: return new Color(1f, 0.84f, 0.18f);      // electric yellow
                case Resonance.Cinder: return new Color(1f, 0.5f, 0.22f);    // ember orange
                case Resonance.Root: return new Color(0.5f, 0.92f, 0.45f);   // moss green
                case Resonance.Cryo: return new Color(0.55f, 0.85f, 1f);     // ice blue
                case Resonance.Prism: return new Color(0.9f, 0.82f, 1f);     // pale lilac-white (apart from Arc's yellow)
                case Resonance.Basic: return new Color(0.78f, 0.76f, 0.72f);  // plain steel
            }
            return UnityEngine.Color.white;
        }

        /// <summary>
        /// A unit's colour (card swirls, HUD lines, flashes) under a resonance: the element's colour, or the unit's own
        /// colour with no resonance or the Basic one.
        /// </summary>
        public static Color ResonanceColor(Color own, Resonance? resonance)
        {
            return resonance.HasValue && resonance.Value != Resonance.Basic ? Color(resonance.Value) : own;
        }

        /// <summary>
        /// The glow a character shows. A neutral-glow member (MC) shows its own glow color until it channels an
        /// element; everyone else glows in their element. Pass <paramref name="channeled"/> once channeling exists.
        /// </summary>
        public static Color GlowFor(CharacterDefinition member, Resonance? channeled = null)
        {
            if (member == null) return UnityEngine.Color.white;
            if (channeled.HasValue) return Color(channeled.Value);
            return member.neutralGlow ? member.glowColor : Color(member.resonance);
        }

        /// <summary>Element label for menus: "MULTI" for members that can channel several elements.</summary>
        public static string LabelFor(CharacterDefinition member)
        {
            if (member != null && member.channels != null && member.channels.Count > 1) return "Multi";
            return member != null ? Display(member.resonance) : "";
        }
    }
}
