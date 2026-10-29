using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Combat.View
{
    /// <summary>
    /// CARDS > BOOK
    /// The card collection's book: an original design drawn in code (dark leather boards with gold corners, cream
    /// parchment pages with a gutter shadow), opened, turned and closed with smooth eased motion.
    ///   Open    the closed book (just its cover) sits where it was; the cover swings over the spine to the left with an
    ///           eased overshoot while the book slides to centre and the left page arrives
    ///   Turn    the page curls over: it's a column of strips, and every piece on it (strips, cards, labels) is bent
    ///           around the spine by <see cref="PageCurl"/>, the inner part leading the outer edge (that lag is the
    ///           curl); a shade sweeps across it and a shadow falls on the page beneath
    ///   Close   the reverse of open
    /// Pages are filled by <see cref="BuildPage"/> (page index, container). Every look and timing number is a constant
    /// below, so it can be tuned. Unscaled time.
    /// </summary>
    public class CardBook : MonoBehaviour
    {
        // CARDS > BOOK > TUNING
        public static readonly Vector2 PageSize = new Vector2(560f, 580f);
        const float Spine = 28f, Margin = 22f;                  // the gap between the pages; the boards beyond the pages
        const float OpenSeconds = 0.28f, CloseSeconds = 0.35f, TurnSeconds = 0.42f;   // open: quick, so the page follows straight on
        const float FirstPageSeconds = 0.36f;                   // the first page turning over (with the cards) as it opens
        const float CurlLag = 0.45f;                            // how far the outer edge trails the spine side (the curl)
        const float CoverLag = 0.08f;                           // the cover is stiff: almost no curl
        const float Depth = 3.2f;                               // virtual camera distance in page widths (perspective)
        const float SlideSeconds = 0.3f;                        // the open book sliding to the middle (after the swing)
        const int Strips = 14;                                  // strips across a turning page (smoothness of the curl)
        static readonly Color Leather = new Color(0.33f, 0.17f, 0.12f), LeatherDark = new Color(0.2f, 0.09f, 0.07f);
        static readonly Color Parchment = new Color(0.96f, 0.91f, 0.78f), ParchmentEdge = new Color(0.84f, 0.76f, 0.58f);
        static readonly Color Gold = new Color(0.88f, 0.7f, 0.32f);
        static readonly Color Shade = new Color(0.55f, 0.48f, 0.38f);

        /// <summary>Fills one page (0-based) into its container (sized <see cref="PageSize"/>, centred).</summary>
        public Action<int, RectTransform> BuildPage;
        public int PageCount = 2;
        public int Spread { get; private set; }
        public int SpreadCount { get { return Mathf.Max(1, (PageCount + 1) / 2); } }
        /// <summary>A turn, open or close is playing (input should wait).</summary>
        public bool Busy { get; private set; }

        RectTransform book, leftPage, rightPage, leftContent, rightContent, sheet, sheetFront, sheetBack, cover, shadow;
        CanvasGroup leftGroup, leftHalf;
        float progress;            // 0..1 through the current turn
        bool forward = true, turning, coverTurn;

        static Texture2D leatherTex, parchmentTex, gradientTex;

        public static Vector2 Size { get { return new Vector2(PageSize.x * 2f + Spine + Margin * 2f, PageSize.y + Margin * 2f); } }

        public static CardBook Create(RectTransform parent)
        {
            var rt = UIFactory.CreateRect("CardBook", parent).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Size);
            var b = rt.gameObject.AddComponent<CardBook>();
            b.book = rt;
            b.Build();
            return b;
        }

        float SpineX { get { return 0f; } }   // the book is centred on its spine
        float PageCentreX(bool right) { return (right ? 1f : -1f) * (Spine / 2f + PageSize.x / 2f); }

        void Build()
        {
            // The boards (leather, behind everything): the left half (board and spine) apart, so a closed book is only
            // its right half.
            var left = UIFactory.CreateRect("LeftHalf", book).Fill();
            leftHalf = left.gameObject.AddComponent<CanvasGroup>();
            var leftBoard = Raw(left, "Board", LeatherTexture(), Leather);
            leftBoard.rectTransform.anchorMin = Vector2.zero;
            leftBoard.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            leftBoard.rectTransform.offsetMin = leftBoard.rectTransform.offsetMax = Vector2.zero;
            leftBoard.uvRect = new Rect(0f, 0f, 0.5f, 1f);
            var rightBoard = Raw(book, "Board", LeatherTexture(), Leather);
            rightBoard.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            rightBoard.rectTransform.anchorMax = Vector2.one;
            rightBoard.rectTransform.offsetMin = rightBoard.rectTransform.offsetMax = Vector2.zero;
            rightBoard.uvRect = new Rect(0.5f, 0f, 0.5f, 1f);
            var spine = UIFactory.CreateImage(left, "Spine", LeatherDark);
            spine.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(Spine + 6f, Size.y));

            leftPage = Page(book, "LeftPage", false);
            rightPage = Page(book, "RightPage", true);
            leftGroup = leftPage.gameObject.AddComponent<CanvasGroup>();
            leftContent = UIFactory.CreateRect("Content", leftPage).Fill();
            rightContent = UIFactory.CreateRect("Content", rightPage).Fill();

            // The shadow a turning page casts on the page beneath it.
            var sh = Raw(book, "TurnShadow", GradientTexture(), new Color(0f, 0f, 0f, 0f));
            shadow = sh.rectTransform;
            shadow.sizeDelta = new Vector2(PageSize.x * 0.5f, PageSize.y);
            shadow.gameObject.SetActive(false);

            // The turning sheet: front and back faces, each a column of parchment strips plus its content.
            sheet = UIFactory.CreateRect("Sheet", book).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Size);
            sheetFront = UIFactory.CreateRect("Front", sheet).Fill();
            sheetBack = UIFactory.CreateRect("Back", sheet).Fill();
            sheet.gameObject.SetActive(false);

            // The front cover, lying over the right page while closed.
            cover = UIFactory.CreateRect("Cover", book).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Size);
            BuildCoverFace(cover, true);
            cover.gameObject.SetActive(false);
        }

        RectTransform Page(RectTransform parent, string name, bool right)
        {
            var page = UIFactory.CreateRect(name, parent).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(PageCentreX(right), 0f), PageSize);
            var paper = Raw(page, "Paper", ParchmentTexture(), Color.white);
            paper.rectTransform.Fill();
            // The gutter: a soft shadow along the spine side.
            var gutter = Raw(page, "Gutter", GradientTexture(), new Color(0.25f, 0.16f, 0.08f, 0.35f));
            gutter.rectTransform.Place(new Vector2(right ? 0f : 1f, 0.5f), new Vector2(right ? 0f : 1f, 0.5f), Vector2.zero, new Vector2(70f, PageSize.y));
            if (!right) gutter.uvRect = new Rect(1f, 0f, -1f, 1f);
            return page;
        }


        // CARDS > BOOK > PAGES

        /// <summary>Shows a spread straight away (no animation).</summary>
        public void ShowSpread(int spread)
        {
            Spread = Mathf.Clamp(spread, 0, SpreadCount - 1);
            Fill(leftContent, Spread * 2);
            Fill(rightContent, Spread * 2 + 1);
        }

        void Fill(RectTransform container, int page)
        {
            UIFactory.ClearChildren(container);
            if (BuildPage != null && page < PageCount) BuildPage(page, container);
        }

        /// <summary>Turns to the next (dir 1) or previous (-1) spread, curling the page over.</summary>
        public IEnumerator Turn(int dir)
        {
            int next = Spread + dir;
            if (Busy || next < 0 || next >= SpreadCount) yield break;
            Busy = true;
            forward = dir > 0;
            coverTurn = false;
            int old = Spread;
            // The sheet's faces: forward, the old right page curling over to become the new left page; backward the
            // other way. The page under the sheet shows the new spread at once.
            SetupSheet(forward ? old * 2 + 1 : old * 2, forward ? next * 2 : next * 2 + 1, true);
            if (forward) Fill(rightContent, next * 2 + 1); else Fill(leftContent, next * 2);
            yield return Animate(TurnSeconds, CurlLag);
            Spread = next;
            Fill(forward ? leftContent : rightContent, forward ? next * 2 : next * 2 + 1);
            EndSheet();
            Busy = false;
        }

        // CARDS > BOOK > OPEN AND CLOSE

        /// <summary>Shows only the book's right half (closed: the cover over the right page) or the whole book.</summary>
        void LeftShown(bool on)
        {
            leftHalf.alpha = on ? 1f : 0f;
            leftGroup.alpha = on ? 1f : 0f;
        }

        /// <summary>Opens the book. Closed, it's only its right half (the cover), grown from <paramref name="from"/> to
        /// full size with the cover where the middle of the screen will be. The cover swings open (its inside, plain
        /// leather, lands on the left board), then the blank first page turns over with the left page's cards already on
        /// its back, uncovering the right page's; only then does the open book slide to the middle.</summary>
        public IEnumerator Open(Vector2 from, float fromScale = 0.25f)
        {
            Busy = true;
            ShowSpread(Spread);
            UIFactory.ClearChildren(rightContent);   // the blank first page, under the cover
            LeftShown(false);
            var home = book.anchoredPosition;
            var shifted = home - new Vector2(PageCentreX(true), 0f);   // the right half where the middle will be
            Vector2 closedAt = from - new Vector2(PageCentreX(true), 0f) * fromScale;
            book.anchoredPosition = closedAt;
            book.localScale = Vector3.one * fromScale;
            cover.gameObject.SetActive(true);
            yield return Run(0.25f, p =>
            {
                float e = Ease(p);
                book.localScale = Vector3.one * Mathf.Lerp(fromScale, 1f, e);
                book.anchoredPosition = Vector2.Lerp(closedAt, shifted, e);
            });
            book.anchoredPosition = shifted;

            // The cover swings over (stiff, with an overshoot); the book stays where it is. Its inside lands on the left
            // board, so only the board shows there (no page yet).
            forward = true;
            coverTurn = true;
            SetupSheet(-1, -1, false);
            cover.gameObject.SetActive(false);
            yield return Animate(OpenSeconds, CoverLag);
            EndSheet();
            leftHalf.alpha = 1f;

            // The first page turns over: blank on the front, the left page's cards on its back; the right page's cards
            // are under it.
            coverTurn = false;
            SetupSheet(-1, Spread * 2, true);
            Fill(rightContent, Spread * 2 + 1);
            yield return Animate(FirstPageSeconds, CurlLag);
            Fill(leftContent, Spread * 2);   // the landed page for real (the sheet's copy goes with the sheet)
            leftGroup.alpha = 1f;
            EndSheet();

            // Only now does the camera move: the open book slides to the middle.
            yield return Run(SlideSeconds, p => book.anchoredPosition = Vector2.Lerp(shifted, home, Ease(p)));
            book.anchoredPosition = home;
            Busy = false;
        }

        /// <summary>Closes the book, the opening backwards: it slides back so its right half is in the middle, the left
        /// page (cards and all) turns back over to the right, the cover swings shut (the left half goes with it) and the
        /// book shrinks toward <paramref name="to"/>.</summary>
        public IEnumerator Close(Vector2 to, float toScale = 0.25f)
        {
            Busy = true;
            var home = book.anchoredPosition;
            var shifted = home - new Vector2(PageCentreX(true), 0f);
            yield return Run(SlideSeconds, p => book.anchoredPosition = Vector2.Lerp(home, shifted, Ease(p)));
            book.anchoredPosition = shifted;

            // The left page turns back over to the right (blank side up), leaving the bare board on the left...
            forward = false;
            coverTurn = false;
            SetupSheet(Spread * 2, -1, true);
            leftGroup.alpha = 0f;
            yield return Animate(FirstPageSeconds, CurlLag);
            UIFactory.ClearChildren(rightContent);
            EndSheet();

            // ...then the cover lifts off the left and swings shut: only the right half is left.
            LeftShown(false);
            coverTurn = true;
            SetupSheet(-1, -1, false);
            yield return Animate(CloseSeconds, CoverLag);
            EndSheet();
            cover.gameObject.SetActive(true);
            Vector2 closedAt = to - new Vector2(PageCentreX(true), 0f) * toScale;
            yield return Run(0.22f, p =>
            {
                float e = Ease(p);
                book.localScale = Vector3.one * Mathf.Lerp(1f, toScale, e);
                book.anchoredPosition = Vector2.Lerp(shifted, closedAt, e);
            });
            cover.gameObject.SetActive(false);
            book.anchoredPosition = home;
            book.localScale = Vector3.one;
            LeftShown(true);
            ShowSpread(Spread);
            Busy = false;
        }

        // CARDS > BOOK > THE TURNING SHEET

        /// <summary>Builds the sheet's two faces. Pages: the content each face carries (-1 = none). Cover turns use the
        /// cover's front and its inside board instead of parchment.</summary>
        void SetupSheet(int frontPage, int backPage, bool parchment)
        {
            UIFactory.ClearChildren(sheetFront);
            UIFactory.ClearChildren(sheetBack);
            // Forward: the front lies on the right page (its content laid out there) and the back lands on the left.
            bool frontRight = forward;
            BuildFace(sheetFront, frontRight, parchment, frontPage, coverTurn && forward);
            BuildFace(sheetBack, !frontRight, parchment, backPage, coverTurn && !forward);
            foreach (var g in sheet.GetComponentsInChildren<Graphic>(true))
            {
                var curl = g.GetComponent<PageCurl>();   // (no ?? on Unity objects: the editor's fake null)
                if (curl == null) curl = g.gameObject.AddComponent<PageCurl>();
                curl.Book = this;
                curl.RightHome = g.transform.IsChildOf(frontRight ? sheetFront : sheetBack);
                // Everything belonging to one card moves together as a flat piece (only the paper strips bend), so cards
                // never come apart or skew while the page turns.
                var lift = g.GetComponentInParent<CardLift>();
                curl.Group = lift != null ? (RectTransform)lift.transform : null;
            }
            sheet.gameObject.SetActive(true);
            sheet.SetAsLastSibling();
            shadow.gameObject.SetActive(!coverTurn);
            turning = true;
            progress = 0f;
            ApplySheet();
        }

        void BuildFace(RectTransform face, bool right, bool parchment, int page, bool isCoverFront)
        {
            var area = UIFactory.CreateRect(right ? "OnRight" : "OnLeft", face).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(PageCentreX(right), 0f), coverTurn ? new Vector2(PageSize.x + Margin, Size.y) : PageSize);
            if (coverTurn)
            {
                // The cover's outside (front of the swing) or its inside: plain leather like the board it lands on.
                if (isCoverFront) BuildCoverFace(area, false);
                else Raw(area, "Inside", null, Leather).rectTransform.Fill();
                for (int i = 0; i < Strips; i++) AddStrip(area, i, null);   // invisible strips keep the bend smooth
                return;
            }
            for (int i = 0; i < Strips; i++) AddStrip(area, i, ParchmentTexture());
            var content = UIFactory.CreateRect("Content", area).Fill();
            if (page >= 0 && BuildPage != null && page < PageCount) BuildPage(page, content);
        }

        void AddStrip(RectTransform area, int i, Texture2D tex)
        {
            var go = new GameObject("Strip" + i, typeof(RectTransform));
            go.transform.SetParent(area, false);
            var raw = go.AddComponent<RawImage>();
            // Plain parchment colour while it moves: textures on bent strips shimmer, so they only show on still pages.
            raw.texture = null;
            raw.color = tex != null ? Parchment : new Color(1f, 1f, 1f, 0f);
            raw.raycastTarget = false;
            if (tex != null) paperStrips.Add(raw);
            float w = 1f / Strips;
            raw.uvRect = new Rect(i * w, 0f, w, 1f);
            var rt = raw.rectTransform;
            rt.anchorMin = new Vector2(i * w, 0f);
            rt.anchorMax = new Vector2((i + 1) * w, 1f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            stripRaws.Add(raw);
        }

        readonly List<RawImage> stripRaws = new List<RawImage>();
        readonly List<RawImage> paperStrips = new List<RawImage>();   // the visible parchment strips (shaded as they turn)

        void EndSheet()
        {
            turning = false;
            sheet.gameObject.SetActive(false);
            shadow.gameObject.SetActive(false);
            UIFactory.ClearChildren(sheetFront);
            UIFactory.ClearChildren(sheetBack);
            stripRaws.Clear();
            paperStrips.Clear();
        }

        IEnumerator Animate(float seconds, float lag, Action<float> alongside = null)
        {
            currentLag = lag;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                progress = t / seconds;
                ApplySheet();
                if (alongside != null) alongside(progress);
                yield return null;
            }
            progress = 1f;
            ApplySheet();
            if (alongside != null) alongside(1f);
        }

        float currentLag = CurlLag;

        /// <summary>Which face shows (the front until the page is past upright), the strip shading, the shadow beneath, and
        /// a redraw of everything on the sheet.</summary>
        void ApplySheet()
        {
            float mid = Theta(PageSize.x * 0.6f);
            // The front starts on the side the sheet leaves (right going forward, left going back).
            bool showFront = forward ? mid < Mathf.PI / 2f : mid > Mathf.PI / 2f;
            sheetFront.gameObject.SetActive(showFront);
            sheetBack.gameObject.SetActive(!showFront);
            foreach (var raw in paperStrips)
            {
                if (raw == null) continue;
                // Shade by how edge-on the strip is to the viewer.
                var r = raw.rectTransform;
                float d = Mathf.Abs(book.InverseTransformPoint(r.TransformPoint(r.rect.center)).x - SpineX);
                float lit = Mathf.Abs(Mathf.Cos(Theta(d)));
                raw.color = Color.Lerp(Shade, Parchment, 0.35f + 0.65f * lit);
            }
            if (!coverTurn && shadow != null)
            {
                // The shadow on the page beneath: strongest mid-turn, on the side the sheet is leaving.
                float s = Mathf.Sin(mid);
                shadow.anchoredPosition = new Vector2((forward ? 1f : -1f) * (Spine / 2f + PageSize.x * 0.25f), 0f);
                ((RawImage)shadow.GetComponent<Graphic>()).uvRect = forward ? new Rect(1f, 0f, -1f, 1f) : new Rect(0f, 0f, 1f, 1f);
                shadow.GetComponent<Graphic>().color = new Color(0f, 0f, 0f, 0.35f * s);
                shadow.SetSiblingIndex(sheet.GetSiblingIndex());
            }
            foreach (var curl in sheet.GetComponentsInChildren<PageCurl>()) curl.Refresh();
        }

        /// <summary>How far round a point of the sheet is (0 = lying on the right page, π = on the left), by its distance
        /// from the spine: the spine side leads and the outer edge trails, so the page curls.</summary>
        float Theta(float d)
        {
            float u = Mathf.Clamp01(progress * (1f + currentLag) - currentLag * Mathf.Clamp01(d / PageSize.x));
            float e = coverTurn ? OverBack(u) : Smooth(u);
            float theta = Mathf.PI * e;
            return forward ? theta : Mathf.PI - theta;
        }

        /// <summary>A point on the sheet, bent round the spine with perspective. <paramref name="rightHome"/>: the face is
        /// laid out on the right page (its distance from the spine is x), else on the left.</summary>
        public Vector3 Curl(RectTransform rt, Vector3 local, bool rightHome)
        {
            Vector3 b = book.InverseTransformPoint(rt.TransformPoint(local));
            float d = Mathf.Max(0f, rightHome ? b.x - SpineX : SpineX - b.x);
            float theta = Theta(d);
            float x = SpineX + d * Mathf.Cos(theta);
            float z = d * Mathf.Sin(theta);
            float cam = PageSize.x * Depth;
            float s = cam / Mathf.Max(1f, cam - z * 0.6f);   // nearer (lifted) parts grow a little
            var p = new Vector3(x, b.y * s, 0f);
            return rt.InverseTransformPoint(book.TransformPoint(p));
        }

        public bool Turning { get { return turning; } }

        // CARDS > BOOK > LOOK (all drawn here)

        void BuildCoverFace(RectTransform parent, bool closedOnBook)
        {
            var area = closedOnBook
                ? UIFactory.CreateRect("Front", parent).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(PageCentreX(true), 0f), new Vector2(PageSize.x + Margin, Size.y))
                : parent;
            // The closed cover has its grain; on the swinging sheet it's flat (textures shimmer while bending).
            var leather = Raw(area, "Leather", closedOnBook ? LeatherTexture() : null, Leather);
            leather.rectTransform.Fill();
            var emblem = UIFactory.CreateImage(area, "Emblem", Gold);
            emblem.sprite = CardIcons.CardBack;
            emblem.preserveAspect = true;
            emblem.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200f, 200f));
            var clasp = UIFactory.CreateImage(area, "Clasp", Gold);
            clasp.rectTransform.Place(new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(18f, 70f));
        }

        static RawImage Raw(RectTransform parent, string name, Texture tex, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var raw = go.AddComponent<RawImage>();
            raw.texture = tex;
            raw.color = color;
            raw.raycastTarget = false;
            return raw;
        }

        static float Smooth(float t) { return t * t * (3f - 2f * t); }
        static float Ease(float t) { t = 1f - t; return 1f - t * t * t; }
        /// <summary>The cover's swing: a small overshoot that settles fast (a big one made a pause before the page turned).</summary>
        static float OverBack(float t)
        {
            const float c1 = 0.6f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        static IEnumerator Run(float seconds, Action<float> step)
        {
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                step(Mathf.Clamp01(t / seconds));
                yield return null;
            }
            step(1f);
        }

        /// <summary>Dark leather with pixel grain (point filtered: reads as pixel art when scaled up).</summary>
        static Texture2D LeatherTexture()
        {
            if (leatherTex != null) return leatherTex;
            const int w = 96, h = 64;
            leatherTex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
            var rng = new System.Random(7);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float n = (float)rng.NextDouble();
                    float v = 0.9f + 0.1f * n + (n > 0.93f ? 0.12f : 0f);
                    leatherTex.SetPixel(x, y, new Color(v, v, v, 1f));
                }
            leatherTex.Apply();
            return leatherTex;
        }

        /// <summary>Cream parchment: soft grain, darker toward the outer edges, a few faint ruled lines.</summary>
        static Texture2D ParchmentTexture()
        {
            if (parchmentTex != null) return parchmentTex;
            const int w = 80, h = 84;
            parchmentTex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var rng = new System.Random(11);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float edge = Mathf.Min(Mathf.Min(x, w - 1 - x), Mathf.Min(y, h - 1 - y));
                    float k = Mathf.Clamp01(edge / 6f);
                    var c = Color.Lerp(ParchmentEdge, Parchment, k);
                    float n = ((float)rng.NextDouble() - 0.5f) * 0.035f;
                    c = new Color(c.r + n, c.g + n, c.b + n, 1f);
                    if (y % 12 == 6 && x > 6 && x < w - 7) c = Color.Lerp(c, ParchmentEdge, 0.25f);
                    parchmentTex.SetPixel(x, y, c);
                }
            parchmentTex.Apply();
            return parchmentTex;
        }

        /// <summary>White fading to clear left to right (gutter and turn shadows; flip the uvRect for the other way).</summary>
        static Texture2D GradientTexture()
        {
            if (gradientTex != null) return gradientTex;
            const int w = 32;
            gradientTex = new Texture2D(w, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int x = 0; x < w; x++)
            {
                float a = 1f - x / (float)(w - 1);
                gradientTex.SetPixel(x, 0, new Color(1f, 1f, 1f, a * a));
            }
            gradientTex.Apply();
            return gradientTex;
        }
    }

    /// <summary>CARDS > BOOK > CURL. Bends one Graphic on the turning sheet round the spine (<see cref="CardBook.Curl"/>);
    /// text moves and scales as a whole so it never skews.</summary>
    public class PageCurl : BaseMeshEffect
    {
        public CardBook Book;
        public bool RightHome;
        /// <summary>The card this graphic belongs to (moved as one flat piece), or null to bend per vertex.</summary>
        public RectTransform Group;

        public void Refresh()
        {
            if (graphic != null) graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || Book == null || !Book.Turning) return;
            var rt = (RectTransform)transform;
            var v = new UIVertex();
            if (Group != null)
            {
                // The card's centre goes where the curl puts it; the card is squeezed across by how edge-on it is.
                Vector3 c = rt.InverseTransformPoint(Group.TransformPoint(Group.rect.center));
                Vector3 to = Book.Curl(rt, c, RightHome);
                Vector3 side = Book.Curl(rt, c + Vector3.right * Mathf.Max(1f, Group.rect.width * 0.5f), RightHome);
                float squeeze = Mathf.Abs((side - to).x) / Mathf.Max(1f, Group.rect.width * 0.5f);
                for (int i = 0; i < vh.currentVertCount; i++)
                {
                    vh.PopulateUIVertex(ref v, i);
                    var off = v.position - c;
                    v.position = to + new Vector3(off.x * squeeze, off.y, 0f);
                    vh.SetUIVertex(v, i);
                }
                return;
            }
            if (graphic is Text)
            {
                Vector3 centre = rt.rect.center;
                Vector3 moved = Book.Curl(rt, centre, RightHome);
                Vector3 side = Book.Curl(rt, centre + Vector3.right * Mathf.Max(1f, rt.rect.width * 0.5f), RightHome);
                float sx = (side - moved).x / Mathf.Max(1f, rt.rect.width * 0.5f);
                for (int i = 0; i < vh.currentVertCount; i++)
                {
                    vh.PopulateUIVertex(ref v, i);
                    var off = v.position - centre;
                    v.position = moved + new Vector3(off.x * Mathf.Abs(sx), off.y, 0f);
                    vh.SetUIVertex(v, i);
                }
                return;
            }
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref v, i);
                v.position = Book.Curl(rt, v.position, RightHome);
                vh.SetUIVertex(v, i);
            }
        }
    }
}
