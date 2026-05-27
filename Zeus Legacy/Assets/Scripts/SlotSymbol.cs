using UnityEngine;

public enum SymbolType
{
    Cherry,
    Lemon,
    Orange,
    Plum,
    Bell,
    Star
}

[System.Serializable]
public class SymbolData
{
    public SymbolType type;
    public Sprite     sprite;
}
