namespace SymphonyRecomp.Android.SecondScreen;

public sealed class MapViewport
{
    public float Zoom { get; private set; } = 4;
    public float CenterX { get; private set; } = 128;
    public float CenterY { get; private set; } = 128;
    public void FocusPlayer(int x, int y)
    {
        Zoom = 4; CenterX = x >= 0 ? x : 128; CenterY = y >= 0 ? y : 128; Clamp();
    }
    public (float X, float Y) Project(float x, float y, float scale)
        => (500 + (x-CenterX)*scale, 505 + (y-CenterY)*scale);
    public void Reset() { Zoom = 1; CenterX = CenterY = 128; }
    public void ScaleBy(float factor)
    {
        if (!float.IsFinite(factor) || factor <= 0) return;
        Zoom = Math.Clamp(Zoom * factor, 1, 8); Clamp();
    }
    public void PanBy(float x, float y)
    {
        if (!float.IsFinite(x) || !float.IsFinite(y)) return;
        CenterX += x; CenterY += y; Clamp();
    }
    void Clamp()
    {
        if (Zoom == 1) { CenterX = CenterY = 128; return; }
        CenterX = Math.Clamp(CenterX, 0, 256);
        CenterY = Math.Clamp(CenterY, 0, 256);
    }
}
