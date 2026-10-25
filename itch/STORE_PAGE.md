# Reverse Shell: itch.io store page

Everything for the itch.io page is in this folder. Paste the text from here or from `description.html`. Open that
file in a browser and copy it, so the headings and bold text paste into itch's editor.

```
itch/
  STORE_PAGE.md          this file: every field, in the order itch's "Edit game" form asks for them
  description.html       the page description (copy it from a browser into itch's editor)
  cover-630x500.png      Cover image (itch's recommended size)
  cover-1260x1000.png    the same at 2x, for sharp screens (itch accepts either)
  banner-960x240.png     header banner (Edit theme → Banner)
  screenshots/           the 5 screenshots for itch's Screenshots field (1920x1080)
  helper-images/         6 pictures that go inside the description (story-*, combat-*)
```

---

## 1. Edit game: the basic fields

| Field | Value |
|---|---|
| **Title** | Reverse Shell |
| **Project URL** | `reverse-shell` (or `reverse-shell-demo`) |
| **Short description or tagline** | A card-merge RPG about the machines that outlived us. Free Chapter One demo, in your browser. |
| **Classification** | Games |
| **Kind of project** | HTML |
| **Release status** | In development |
| **Pricing** | No payments. Keep it free: the Raven icons are licensed for free games only. |

### Uploads
- Upload `builds/webgl/demo-web-build-main.zip`. `index.html` is already at the zip's root.
- Tick **This file will be played in the browser**.
- Build the release with **Development Build off**. The screenshots came from a development build. Its
  "Development Build" label is painted out of them, but players will still see it in the game.

### Embed options
| Option | Value |
|---|---|
| Viewport dimensions | **960 x 540** (the build's size) |
| Automatically start on page load | **Off** (players click to launch, which also gives the game keyboard focus) |
| Fullscreen button | **On** |
| Mobile friendly | **Off** (it needs a keyboard and mouse) |
| Enable scrollbars | Off |
| SharedArrayBuffer support | Off (Unity 2019 doesn't use it) |

### Details
| Field | Value |
|---|---|
| **Genre** | Role Playing |
| **Tags** (itch allows 10) | `card-game`, `turn-based`, `pixel-art`, `rpg`, `post-apocalyptic`, `robots`, `story-rich`, `singleplayer`, `fangame`, `unity` |
| **App store links** | none |
| **Custom noun** | leave empty |

### Metadata (under "Details", the optional extras)
| Field | Value |
|---|---|
| Made with | Unity |
| Inputs | Keyboard, Mouse |
| Average session | About a half-hour (check this after your play-through) |
| Languages | English |
| Accessibility | Leave unticked. The game has no colour-blind mode or remapping yet. |
| Links | Add your feedback form here, when you have one |

### Screenshots (upload in this order)
itch shows them down the right of the page.

| # | File (in `screenshots/`) | Shows |
|---|---|---|
| 1 | `01-title-screen.png` | The title screen with its falling cards |
| 2 | `02-overgrown-plaza.png` | Walking the sunlit plaza in the ruins |
| 3 | `03-pause-menu.png` | The pause menu as a mat of physical cards |
| 4 | `04-pre-combat.png` | Prepare for Battle: party slots, backup, the resonance chart |
| 5 | `05-unit-inspect.png` | Holding a unit to inspect its stats and cards |

### Helper images (inside the description)
`description.html` puts these between the paragraphs. Its Copy button leaves an `[insert image: ...]` line at each
spot, and you upload the picture there with the editor's image button.

| File (in `helper-images/`) | Goes after |
|---|---|
| `story-everlasting.png` | "...*Live once. Live on.*" |
| `story-awakening.png` | "...face the guardian at the heart of Mossline." |
| `combat-battle.png` | the "Every card is a decision" heading |
| `combat-card-merge.png` | the combat bullet list |
| `combat-turn-playing-out.png` | the merge picture |
| `combat-card-detail.png` | the turn picture |

Optional: a short trailer or GIF of a turn playing out (cards flying into the queue, hits, merges) would do more
than any still. It can go in the "Gameplay video or trailer" field (YouTube link) or at the top of the description.

---

## 2. Edit theme (the "Edit theme" button on the page itself)

Matches the game's ink-and-gold UI (`InkStyle`):

| Setting | Value |
|---|---|
| Layout | Embed at the top, description under it, screenshots on the right |
| Banner | `banner-960x240.png`, aligned left |
| Background | `#1B1A18` (Ink) |
| Background 2 (the page column) | `#24221F` |
| Text | `#ECE5D3` (Paper) |
| Link | `#C2A061` (Gold) |
| Button | `#C2A061` with dark text |
| Border | `#3A362F` |
| Header font | a pixel or mono font if itch lists one (the game uses *monogram*). Otherwise a serif such as Cormorant |
| Body font | the default sans, or a serif to suit the game |
| Hide title | **On**, because the banner already shows it |

---

## 3. Visibility (for a friends-only demo first)
- **Draft** while you set it up. itch gives you a secret preview URL you can share.
- When it's ready: **Restricted** (only people with the link or a password), or **Public**.
- Before sharing, open the page in a private window and play 10 minutes (checklist §6).

---

## 4. The description (plain-text copy)

`description.html` has the same text, formatted. This copy is for anywhere that takes plain text.

> *Sunlight through a broken roof. Wind in the grass that grew over the road. Somewhere in the green, a machine is
> still keeping watch.*

**Reverse Shell** is a narrative RPG with tactical card combat, set in a beautiful world humanity left behind.

Long ago, people found a way to live forever. **Reverse Shell** recorded a mind onto a **Soul Card** and placed it
in a mechanical body built to outlast anything. *Live once. Live on.*

Now the cities are overgrown, and you wake alone beside a cracked capsule. You're a machine with a human's name
that you can barely remember. Explore the ruins, meet the machines that outlived their makers, and face the
guardian at the heart of Mossline.

**This is a free demo of Chapter One, playable in your browser.**

### Every card is a decision
- **Plan, then press Go.** Each fighter standing gives you 1 AP. Every card costs 1 AP and waits in the queue,
  and you can take the whole turn back before you commit.
- **Merge cards.** Drag a card next to a matching one and they fuse into a stronger rank.
- **Charge your Ultimate.** Cards build Ult pips. Spend them at the right moment.
- **Read the field.** Hold any card or unit to see its numbers, statuses and upgrades. Play to the
  **resonance chart**: Electric, Fire, Toxin and Earth each beat the next, and Light and Void beat each other.

### In the demo
- **Chapter One: The Overgrown City.** Wake up alone, piece together records and memory fragments, follow a scanner
  trace through the ruins, face the Collector, and find what the guardian is protecting.
- **Sandbox Mode** (title screen, or Esc → Settings): practice fights at any enemy level with the whole test crew,
  including **Heal Bot**, who keeps everyone running, and **Lucy**, who *infects* things.
- A pause menu dealt out as real cards, three save slots, and Fast or Pretty graphics.

### Controls
| | |
|---|---|
| **Click the game once** | gives it keyboard focus |
| **WASD / arrows** | walk (hold **Shift** to run) |
| **F** | talk, open, interact |
| **Q** | scan (once you have the scanner) |
| **Esc** or **P** | menu (in browser fullscreen, Esc leaves fullscreen, so use **P**) |
| **Click** a card | play it (1 AP) |
| **Drag** a card | move it, or merge it (1 AP) |
| **Hold** a card or unit | read it |
| **Go** / **Enter** | play the turn |
| **Tab** | battle speed |

The full list is in **Settings → Tutorials**.

### Tips for the browser
- Saves are kept in your browser, so a reload keeps them. Clearing site data or using a private window loses them.
- Graphics start on **Fast**, which runs best in a browser. Switch to **Pretty** in Settings for the full look.
- Chrome or Firefox on a desktop or laptop is best. Phones aren't supported.

### Feedback
This is an early demo and the numbers are first drafts. Bugs, balance and "this confused me" are all welcome in the
comments below. Please mention the version on the title screen (e.g. *Demo v0.2*).

### Credits
Made by **Oliver Pearce**. A free, non-commercial fan game inspired by *Reverse: 1999* (not affiliated with
Bluepoch).
- **Icons:** Raven Fantasy Icons by Clockwork Raven Studios
- **Character sprites:** Fantasy Knight by aamatniekss
- **Effects:** Super Pixel Effects Gigapack by Will Tice / unTied Games
- **Font:** monogram by datagoblin
