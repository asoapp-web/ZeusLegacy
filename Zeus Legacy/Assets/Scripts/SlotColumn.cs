using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// ─────────────────────────────────────────────────────────────────────
//  BUFFER LAYOUT — read this once, it explains every index decision
// ─────────────────────────────────────────────────────────────────────
//
//  The symbolBuffer has (visibleRows + 2) slots = 7 slots total.
//  The cellRects / cellImages lists have the same 7 entries.
//
//  Index 0            = hidden cell ABOVE the mask  (never visible)
//  Index 1 .. 5      = the 5 VISIBLE rows (row 0 = index 1, row 4 = index 5)
//  Index 6            = hidden cell BELOW the mask  (never visible)
//
//  ALL three operations that touch a "row" must use the same mapping:
//
//      buffer index = row + 1
//
//  GetSymbolAt(row)     → symbolBuffer[row + 1]   ← reads what player sees
//  PlayWinAnimation(row)→ cellRects   [row + 1]   ← animates the right cell
//  StopSpin writes      → symbolBuffer[1 .. 5]    ← visible slots only
//
//  The OLD bug: GetSymbolAt used symbolBuffer[row] (off by one) while
//  PlayWinAnimation used cellRects[row + 1]. They pointed to different
//  cells, so win detection saw different symbols than the player saw,
//  and animations fired on wrong cells.
// ─────────────────────────────────────────────────────────────────────

public class SlotColumn : MonoBehaviour
{
    // ─────────────────────────────────────────────
    //  Inspector
    // ─────────────────────────────────────────────
    [Header("References")]
    public RectTransform symbolContainer;
    public GameObject    symbolCellPrefab;

    [Header("Win Effect")]
    public GameObject blastRingPrefab;
    public Canvas     rootCanvas;

    [Header("Layout Settings")]
    public int   visibleRows = 5;
    public float cellHeight  = 140f;

    [Tooltip("Moves all rows up (negative) or down (positive). Adjust to fix grid position.")]
    public float containerVerticalOffset = -20f;

    // ─────────────────────────────────────────────
    //  Private State
    // ─────────────────────────────────────────────
    private List<RectTransform> cellRects    = new List<RectTransform>();
    private List<Image>         cellImages   = new List<Image>();
    private List<SymbolType>    symbolBuffer = new List<SymbolType>();
    private SymbolData[]        allSymbols;

    private bool  isSpinning    = false;
    private float scrollSpeed   = 0f;
    private float currentOffset = 0f;

    // ─────────────────────────────────────────────
    //  Initialization
    // ─────────────────────────────────────────────

    public void Initialize(SymbolData[] symbols)
    {
        allSymbols = symbols;

        // 7 cells total: 1 hidden top + 5 visible + 1 hidden bottom
        int cellCount = visibleRows + 2;

        for (int i = 0; i < cellCount; i++)
        {
            GameObject cell = Instantiate(symbolCellPrefab, symbolContainer);
            cellRects.Add(cell.GetComponent<RectTransform>());
            cellImages.Add(cell.GetComponent<Image>());
        }

        symbolContainer.sizeDelta = new Vector2(
            symbolContainer.sizeDelta.x,
            cellHeight * cellCount
        );

        RandomizeBuffer();
        SnapCellPositions();
        UpdateCellVisuals();
    }

    // ─────────────────────────────────────────────
    //  Layout
    // ─────────────────────────────────────────────

    void SnapCellPositions()
    {
        for (int i = 0; i < cellRects.Count; i++)
        {
            cellRects[i].anchoredPosition = new Vector2(
                0f,
                -i * cellHeight + containerVerticalOffset
            );
            cellRects[i].sizeDelta = new Vector2(cellHeight, cellHeight);
        }
    }

    // ─────────────────────────────────────────────
    //  Buffer / Visual Helpers
    // ─────────────────────────────────────────────

    void RandomizeBuffer()
    {
        symbolBuffer.Clear();
        for (int i = 0; i < cellRects.Count; i++)
            symbolBuffer.Add(GetRandomSymbol());
    }

    SymbolType GetRandomSymbol()
        => allSymbols[Random.Range(0, allSymbols.Length)].type;

    Sprite GetSprite(SymbolType type)
    {
        foreach (var s in allSymbols)
            if (s.type == type) return s.sprite;
        return null;
    }

    void UpdateCellVisuals()
    {
        for (int i = 0; i < cellImages.Count; i++)
            cellImages[i].sprite = GetSprite(symbolBuffer[i]);
    }

    void ApplyContainerOffset()
    {
        symbolContainer.anchoredPosition = new Vector2(0f, -currentOffset);
    }

    // ─────────────────────────────────────────────
    //  Public Accessor  ← FIXED
    // ─────────────────────────────────────────────

    /// <summary>
    /// Returns the symbol the player can SEE at visible row [row].
    /// row 0 = top visible row, row 4 = bottom visible row.
    ///
    /// FIX: was symbolBuffer[row] — now symbolBuffer[row + 1]
    /// because buffer index 0 is the hidden top cell, so visible
    /// row 0 lives at buffer index 1.
    /// </summary>
    public SymbolType GetSymbolAt(int row)
    {
        int bufIdx = row + 1;   // ← THE FIX (was: return symbolBuffer[row])
        if (bufIdx < 0 || bufIdx >= symbolBuffer.Count)
            return default;
        return symbolBuffer[bufIdx];
    }

    public Vector3 GetCellWorldPosition(int row)
    {
        int bufIdx = row + 1;
        if (bufIdx < 0 || bufIdx >= cellRects.Count) return transform.position;
        return cellRects[bufIdx].position;
    }

    // ─────────────────────────────────────────────
    //  Spinning
    // ─────────────────────────────────────────────

    public void StartSpin(float speed)
    {
        if (isSpinning) return;
        isSpinning  = true;
        scrollSpeed = speed;
        StartCoroutine(SpinLoop());
    }

    IEnumerator SpinLoop()
    {
        while (isSpinning)
        {
            currentOffset += scrollSpeed * Time.deltaTime;

            if (currentOffset >= cellHeight)
            {
                currentOffset -= cellHeight;
                symbolBuffer.RemoveAt(symbolBuffer.Count - 1);
                symbolBuffer.Insert(0, GetRandomSymbol());
            }

            ApplyContainerOffset();
            UpdateCellVisuals();
            yield return null;
        }
    }

    // ─────────────────────────────────────────────
    //  Stop  ← FIXED
    // ─────────────────────────────────────────────

    /// <summary>
    /// Decelerates and snaps to result[].
    /// result[0] = what player sees at row 0 (top),
    /// result[4] = what player sees at row 4 (bottom).
    ///
    /// FIX: was writing result into symbolBuffer[0..4].
    /// Now writes into symbolBuffer[1..5] so that the VISIBLE
    /// cells (buffer indices 1–5) hold the correct symbols,
    /// and GetSymbolAt() reads from the same indices.
    /// </summary>
    public IEnumerator StopSpin(SymbolType[] result, float decelTime)
    {
        // Write result into the VISIBLE portion of the buffer (indices 1..visibleRows)
        for (int i = 0; i < result.Length; i++)
            symbolBuffer[i + 1] = result[i];   // ← FIX: was symbolBuffer[i]

        float startSpeed = scrollSpeed;
        float elapsed    = 0f;

        while (elapsed < decelTime)
        {
            elapsed      += Time.deltaTime;
            scrollSpeed   = Mathf.Lerp(startSpeed, 0f, elapsed / decelTime);
            currentOffset += scrollSpeed * Time.deltaTime;
            if (currentOffset >= cellHeight) currentOffset -= cellHeight;
            ApplyContainerOffset();
            UpdateCellVisuals();
            yield return null;
        }

        scrollSpeed   = 0f;
        currentOffset = 0f;
        symbolContainer.anchoredPosition = Vector2.zero;
        isSpinning = false;

        // Re-apply to guarantee visible slots are correct after decel drift
        for (int i = 0; i < visibleRows; i++)
            symbolBuffer[i + 1] = result[i];   // ← FIX: was symbolBuffer[i]

        UpdateCellVisuals();
    }

    // ─────────────────────────────────────────────
    //  Win Animation
    // ─────────────────────────────────────────────
    //
    //  Phase 1  0.00–0.15 s  Glow flash   — color pings white → yellow → white
    //  Phase 2  0.15–0.55 s  Scale punch  — grows to 150%, bounces to 115%
    //  Phase 3  0.55–0.75 s  Shake        — random ±10px jitter, fades out
    //  Phase 4  0.75 s async Blast ring   — ring expands + fades (runs in parallel)
    //  Phase 5  0.75–1.15 s  Shrink+fade  — scale→0, alpha→0 with ease-in
    //
    //  bufIdx = row + 1 matches GetSymbolAt and StopSpin — all three now agree.

    public IEnumerator PlayWinAnimation(int row)
    {
        int bufIdx = row + 1;   // same offset as GetSymbolAt
        if (bufIdx < 0 || bufIdx >= cellRects.Count) yield break;

        RectTransform rt  = cellRects[bufIdx];
        Image         img = cellImages[bufIdx];

        Color   origColor = img.color;
        Vector2 origPos   = rt.anchoredPosition;
        rt.localScale     = Vector3.one;

        // Phase 1 — Glow flash
        float glowDur   = 0.15f;
        Color glowColor = new Color(1f, 0.95f, 0.25f);
        float t = 0f;
        while (t < glowDur)
        {
            t  += Time.deltaTime;
            img.color = Color.Lerp(origColor, glowColor,
                                   Mathf.PingPong(t / glowDur * 2f, 1f));
            yield return null;
        }
        img.color = origColor;

        // Phase 2 — Scale punch
        float punchDur    = 0.40f;
        float maxScale    = 1.50f;
        float settleScale = 1.15f;
        t = 0f;
        while (t < punchDur)
        {
            t += Time.deltaTime;
            float norm = t / punchDur;
            float s = norm < 0.5f
                ? Mathf.Lerp(1f,        maxScale,    norm * 2f)
                : Mathf.Lerp(maxScale,  settleScale, (norm - 0.5f) * 2f);
            rt.localScale = new Vector3(s, s, 1f);
            yield return null;
        }

        // Phase 3 — Shake
        float shakeDur       = 0.20f;
        float shakeIntensity = 10f;
        t = 0f;
        while (t < shakeDur)
        {
            t += Time.deltaTime;
            float decay = 1f - (t / shakeDur);
            rt.anchoredPosition = origPos + new Vector2(
                Random.Range(-1f, 1f) * shakeIntensity * decay,
                Random.Range(-1f, 1f) * shakeIntensity * decay
            );
            yield return null;
        }
        rt.anchoredPosition = origPos;

        // Phase 4 — Blast ring (async, doesn't block phase 5)
        if (blastRingPrefab != null && rootCanvas != null)
            StartCoroutine(SpawnBlastRing(rt.position));

        // Phase 5 — Shrink + fade
        float shrinkDur = 0.40f;
        t = 0f;
        while (t < shrinkDur)
        {
            t += Time.deltaTime;
            float eased = (t / shrinkDur) * (t / shrinkDur);
            rt.localScale = new Vector3(
                Mathf.Lerp(settleScale, 0f, eased),
                Mathf.Lerp(settleScale, 0f, eased), 1f);
            img.color = new Color(origColor.r, origColor.g, origColor.b,
                                  Mathf.Lerp(1f, 0f, eased));
            yield return null;
        }

        rt.localScale = Vector3.zero;
        img.color     = new Color(origColor.r, origColor.g, origColor.b, 0f);
    }

    IEnumerator SpawnBlastRing(Vector3 worldPos)
    {
        GameObject    ring = Instantiate(blastRingPrefab, rootCanvas.transform);
        RectTransform rrt  = ring.GetComponent<RectTransform>();
        Image         rImg = ring.GetComponent<Image>();

        rrt.position  = worldPos;
        rrt.sizeDelta = new Vector2(cellHeight, cellHeight);

        Color startColor = new Color(1f, 0.85f, 0.2f,  0.9f);
        Color endColor   = new Color(1f, 0.4f,  0.05f, 0f);

        float dur = 0.50f;
        float t   = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float norm     = t / dur;
            rrt.localScale = Vector3.Lerp(Vector3.one * 0.4f, Vector3.one * 2.5f, norm);
            if (rImg != null) rImg.color = Color.Lerp(startColor, endColor, norm);
            yield return null;
        }
        Destroy(ring);
    }

    // ─────────────────────────────────────────────
    //  Cascade  ← FIXED
    // ─────────────────────────────────────────────

    /// <summary>
    /// Removes winning cells from the visible portion of the buffer
    /// (indices 1..visibleRows), fills new randoms at top, then drops.
    ///
    /// FIX: was reading symbolBuffer[row] for surviving symbols.
    /// Now reads symbolBuffer[row + 1] — the visible slots.
    /// </summary>
    public IEnumerator Cascade(bool[] winningRows, float dropDuration)
    {
        // Collect surviving symbols from VISIBLE buffer slots (index 1..visibleRows)
        List<SymbolType> surviving = new List<SymbolType>();
        for (int row = 0; row < visibleRows; row++)
        {
            if (!winningRows[row])
                surviving.Add(symbolBuffer[row + 1]);   // ← FIX: was symbolBuffer[row]
        }

        int newCount = visibleRows - surviving.Count;
        List<SymbolType> newBuffer = new List<SymbolType>();
        for (int i = 0; i < newCount; i++)
            newBuffer.Add(GetRandomSymbol());
        newBuffer.AddRange(surviving);

        // Write back into visible slots (indices 1..visibleRows)
        for (int i = 0; i < visibleRows; i++)
            symbolBuffer[i + 1] = newBuffer[i];         // ← FIX: was symbolBuffer[i]

        // Reset visual state
        foreach (var r  in cellRects)  r.localScale  = Vector3.one;
        foreach (var im in cellImages) im.color       = Color.white;
        UpdateCellVisuals();

        // Drop animation
        float startY  = cellHeight * newCount;
        float elapsed = 0f;
        while (elapsed < dropDuration)
        {
            elapsed += Time.deltaTime;
            float norm = Mathf.SmoothStep(0f, 1f, elapsed / dropDuration);
            symbolContainer.anchoredPosition = new Vector2(0f, Mathf.Lerp(startY, 0f, norm));
            yield return null;
        }
        symbolContainer.anchoredPosition = Vector2.zero;
    }
}
