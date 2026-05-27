using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
//using UnityEditor.Search;
using System;


public class SlotManager : MonoBehaviour
{
    // ─────────────────────────────────────────────
    //  Inspector
    // ─────────────────────────────────────────────
    [Header("Columns — assign 5 SlotColumn objects (left → right)")]
    public SlotColumn[] columns;   // 5 elements

    [Header("Symbol Definitions — add 6 entries")]
    public SymbolData[] symbolDefs;

    [Header("Spin Settings")]
    public float minSpinSpeed    = 600f;
    public float maxSpinSpeed    = 1200f;
    public float minSpinDuration = 1.0f;
    public float maxSpinDuration = 2.5f;
    public float decelDuration   = 0.45f;

    [Header("Cascade Settings")]
    public float cascadeDropDuration = 0.55f;

    [Header("Win Condition")]
    [Tooltip("How many of the same symbol must appear on the grid to count as a win.")]
    public int winThreshold = 7;

    [Header("Rewards")]
    public int startingCoins     = 100;
    public int betCost           = 10;
    public int coinsPerWinSymbol = 10;  // awarded per matching symbol found

    [Header("UI References")]
    public Button          spinButton;
    public TextMeshProUGUI coinText;
    public TextMeshProUGUI winBannerText;
    public GameObject      winBannerPanel;

    // ─────────────────────────────────────────────
    //  Constants
    // ─────────────────────────────────────────────

    // FIX 4: ROWS is now a named constant = 5.
    // Previously the code had a broken property "int visibleRows => ... ? 5 : 5"
    // which always returned 5 regardless. Now it's explicit and shared.
    private const int ROWS = 5;
    private const int COLS = 5;

    // ─────────────────────────────────────────────
    //  Private State
    // ─────────────────────────────────────────────
    private int  coins     = 0;
    private bool isPlaying = false;


    [SerializeField] AudioSource soundSlot;
    [SerializeField] AudioSource soundPop;
    // ─────────────────────────────────────────────
    //  Unity Lifecycle
    // ─────────────────────────────────────────────

    void Start()
    {
        coins = startingCoins;

        // Tell every SlotColumn to build its cell pool
        foreach (var col in columns)
            col.Initialize(symbolDefs);

        if (winBannerPanel != null) winBannerPanel.SetActive(false);

        UpdateCoinUI();
        spinButton.onClick.AddListener(OnSpinPressed);
    }

    // ─────────────────────────────────────────────
    //  Spin Entry Point
    // ─────────────────────────────────────────────

    void OnSpinPressed()
    {
        if (isPlaying)       return;
        if (coins < betCost) { Debug.Log("Not enough coins!"); return; }

        coins -= betCost;
        UpdateCoinUI();
        StartCoroutine(SpinRoutine());
    }

    // ─────────────────────────────────────────────
    //  Main Spin Coroutine
    // ─────────────────────────────────────────────

    IEnumerator SpinRoutine()
    {
        isPlaying = true;
        spinButton.interactable = false;
        Debug.Log("Spinning On");
        soundSlot.Play();
        // 1. Start all columns at random speeds with random stop times
        float[] spinDurations = new float[COLS];
        for (int i = 0; i < COLS; i++)
        {
            float speed = UnityEngine.Random.Range(minSpinSpeed, maxSpinSpeed);
            spinDurations[i] = UnityEngine.Random.Range(minSpinDuration, maxSpinDuration);
            columns[i].StartSpin(speed);
        }

        // 2. Pre-generate what each column will show when it stops
        //    result[col][row] — row 0 = top, row 4 = bottom
        SymbolType[][] result = new SymbolType[COLS][];
        for (int col = 0; col < COLS; col++)
        {
            result[col] = new SymbolType[ROWS];
            for (int row = 0; row < ROWS; row++)
                result[col][row] = symbolDefs[UnityEngine.Random.Range(0, symbolDefs.Length)].type;
        }

        // 3. Stop each column after its individual delay (columns stop left→right)
        for (int i = 0; i < COLS; i++)
            StartCoroutine(StopColumnAfterDelay(i, spinDurations[i], result[i]));

        float maxDuration = 0f;
        foreach (var d in spinDurations) if (d > maxDuration) maxDuration = d;
        yield return new WaitForSeconds(maxDuration + decelDuration + 0.1f);

        // 4. Run the cascade loop (win check → animate → remove → drop → repeat)
        yield return StartCoroutine(CascadeLoop());

        spinButton.interactable = true;
        isPlaying = false;
        Debug.Log("Spinning Off");
        soundSlot.Stop();
    }

    IEnumerator StopColumnAfterDelay(int colIdx, float delay, SymbolType[] colResult)
    {
        yield return new WaitForSeconds(delay);
        yield return StartCoroutine(columns[colIdx].StopSpin(colResult, decelDuration));
    }

    // ─────────────────────────────────────────────
    //  Cascade Loop
    // ─────────────────────────────────────────────

    IEnumerator CascadeLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(0.25f);

            // ── FIX 1: New win check ─────────────────────────────────────────
            // Returns a 2D bool grid [col, row] marking every winning cell.
            // Also returns which symbol type won (for the banner message).
            SymbolType winningType;
            bool[,] winGrid = CheckWins(out winningType);

            // Count how many cells are marked as winning
            int totalWinCells = 0;
            for (int c = 0; c < COLS; c++)
                for (int r = 0; r < ROWS; r++)
                    if (winGrid[c, r]) totalWinCells++;

            if (totalWinCells == 0) break;  // No wins this round — stop looping

            // Award coins proportional to how many winning symbols were found
            int earned = totalWinCells * coinsPerWinSymbol;
            coins += earned;
            UpdateCoinUI();
            ShowWinBanner(winningType, totalWinCells, earned);

            // ── Launch all win animations in parallel ─────────────────────────
            // Every winning cell across all columns starts its animation at the
            // same moment. We collect the coroutines and wait for all of them.
            List<Coroutine> winAnims = new List<Coroutine>();

            for (int col = 0; col < COLS; col++)
            {
                for (int row = 0; row < ROWS; row++)
                {
                    if (winGrid[col, row])
                    {
                        int c = col, r = row;
                        winAnims.Add(StartCoroutine(columns[c].PlayWinAnimation(r)));
                    }
                }
            }

            // Wait until every animation has fully finished (~1.15 s)
            foreach (var anim in winAnims)
                yield return anim;

            HideWinBanner();

            // ── Cascade: per-column, pass which rows that column lost ─────────
            // Build a per-column bool[ROWS] from the 2D winGrid.
            List<Coroutine> cascades = new List<Coroutine>();
            for (int col = 0; col < COLS; col++)
            {
                bool[] colWinRows = new bool[ROWS];
                for (int row = 0; row < ROWS; row++)
                    colWinRows[row] = winGrid[col, row];

                int c = col;
                cascades.Add(StartCoroutine(columns[c].Cascade(colWinRows, cascadeDropDuration)));
            }
            foreach (var cr in cascades)
                yield return cr;

            yield return new WaitForSeconds(0.2f);
        }
    }

    bool[,] CheckWins(out SymbolType winningType)
    {
        // Step 1: count every symbol type on the grid
        Dictionary<SymbolType, int> counts = new Dictionary<SymbolType, int>();
        foreach (var def in symbolDefs)
            counts[def.type] = 0;

        for (int col = 0; col < COLS; col++)
            for (int row = 0; row < ROWS; row++)
                counts[columns[col].GetSymbolAt(row)]++;

        // Step 2: find which symbol (if any) hit the threshold with most matches
        winningType = default;
        int bestCount = 0;

        foreach (var kv in counts)
        {
            if (kv.Value >= winThreshold && kv.Value > bestCount)
            {
                bestCount   = kv.Value;
                winningType = kv.Key;
            }
        }

        // Step 3: build the result grid — mark every cell of the winning symbol
        bool[,] grid = new bool[COLS, ROWS];

        if (bestCount == 0) return grid;  // no winner
        else 
        {
            Debug.Log("Sound Pop");
            soundPop.Play();
        }

        for (int col = 0; col < COLS; col++)
            for (int row = 0; row < ROWS; row++)
                if (columns[col].GetSymbolAt(row) == winningType)
                    grid[col, row] = true;

        return grid;
    }

    // ─────────────────────────────────────────────
    //  UI Helpers
    // ─────────────────────────────────────────────

    void UpdateCoinUI()
    {
        if (coinText != null)
            coinText.text = $"{coins}";
    }

    void ShowWinBanner(SymbolType type, int count, int earned)
    {
        if (winBannerPanel != null) winBannerPanel.SetActive(true);
        if (winBannerText  != null)
            winBannerText.text = $"{type}  ×{count}  WIN!  +{earned}";
    }

    void HideWinBanner()
    {
        if (winBannerPanel != null) winBannerPanel.SetActive(false);
    }
}
