# Raven Fantasy Icons (Clockwork Raven Studios)

- Source: https://clockworkraven.itch.io/raven-fantasy-icons ("Free - Raven Fantasy Icons.zip").
- Only the 32x32 separated files and the artist's note were extracted here.
- **Licence (free version): personal use only.** Personal use means games released for free, with no
  microtransactions or paid ads. A commercial release needs the premium pack.
- Modifying the icons is allowed. **Redistributing or repacking the asset is not allowed** without the creator's
  permission, so keep this folder out of any public repository.
- Credit isn't required but is appreciated: "Icons: Raven Fantasy Icons by Clockwork Raven Studios".

## Icons used in game

`Tools/IconForge/card_icons.py` builds the picked icons into `Resources/UI/CardIcons.png`:

| Use | Icon |
|---|---|
| Attack | fb672 |
| Debuff | fb884 |
| Buff | fb865 |
| Counter | fb665 |
| Heal | fb661 |
| Ultimate | fb776 |
| Defense (picked 2026-10-28) | fb597 |
| Ult pip (the empty pip is a greyed copy) | fb2186 |
| Rank star | fb984 |
| Resonance orbs (Arc, Cinder, Root, Cryo, Prism; picked 2026-10-28) | fb2118, fb2117, fb2120, fb2119, fb2121 |
| CHIP | fb2115 |
| CORE | fb2139 |
| Poison | fb691 |
| ATK Up | fb684 |
| ATK Down | fb700 |
| Physical DEF Down | fb698 |
| DEF Up | fb1899 |
| Stunned | fb688 |
| Regen | fb681 |
| Incantation Might Up | fb683 |
| Burn / Chill / Frozen | fb693, fb694, fb696 |
| Shield / Counter / Covered | fb692, fb699, fb702 |
| Go button chevrons (turned to point right) | fb649 |
| Close X | fb656 |
| Battle queue: moved-card slot (tinted gray) | fb2061 |
| Sun card (rare binding) | fb990 |
| Moon card (rare binding) | fb978 |
| Star card (rare binding) | fb986 |
| Pause sign (drawn by the script in fb647's silvers, fb656's outline) | fb647, fb656 |

## Card icons

`Tools/CardForge/card_forge.py` (`RAVEN_PICKS`) puts these into `Resources/Cards/CardArt.png` as each card's centre
icon, with the cards' 1 px plum outline added. The table below is the older cards; the 110-card pool's picks (2026-10-28, from the icon picker) are in `RAVEN_PICKS` and in the card sheet's Icon column (Planning/CARDS.xlsx). No two cards share an icon:

| Card | Icon |
|---|---|
| Edge Strike | fb724 |
| Cross Slash | fb155 |
| Sync Field | fb1047 |
| Arc Edge | fb731 |
| Static Field | fb1039 |
| Bramble Lash | fb1067 |
| Overgrowth | fb1110 |
| Sap Mend | fb1073 |
| Virus Spike | fb741 |
| Corrupt | fb1117 |
| Worm | fb342 |
| Shield Bash | fb1811 |
| Bulwark | fb2151 |
| Corroded Blade | fb722 |
| Network Link | fb905 |
| Golden Hammer | fb1044 |
| Reinforce | fb1921 |
| Bite | fb799 |
| Coil Up | fb824 |
| Grapple Hook | fb393 |
| Poison Strike | fb835 |
| Spark | fb1025 |
| Static Coil | fb1027 |
| Suppressing Shot | fb1034 |
| Tail Lash | fb1057 |
| Thunder Breath | fb832 |
| Esc menu: Quit | fb3 |
| Esc menu: Save | fb10 |
| Esc menu: Settings | fb2 |
| Esc menu: Party | fb14 |
| Esc menu: Items | fb1 |
| Scanner HUD (binoculars slot) | fb297 |

Units, Resonance and the lock keep their CardForge drawings (the user's choice).
