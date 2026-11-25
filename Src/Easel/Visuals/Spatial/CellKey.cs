namespace Easel.Visuals.Spatial;

// 空间网格的格子坐标。
internal readonly struct CellKey : IEquatable<CellKey>
{
    internal CellKey(int x, int y)
    {
        X = x;
        Y = y;
    }

    internal readonly int X;

    internal readonly int Y;

    public bool Equals(CellKey other) => X == other.X && Y == other.Y;

    public override bool Equals(object? obj) => obj is CellKey key && Equals(key);

    public override int GetHashCode() => unchecked((X * 397) ^ Y);

    public override string ToString() => $"CellKey({X}, {Y})";
}
