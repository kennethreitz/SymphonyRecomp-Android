namespace SymphonyRecomp.Android.SecondScreen;

public sealed class MapViewport
{
    public float Zoom { get; private set; } = 1;
    public float CenterX { get; private set; } = 128;
    public float CenterY { get; private set; } = 128;
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
        float edge = 128 / Zoom;
        CenterX = Math.Clamp(CenterX, edge, 256-edge);
        CenterY = Math.Clamp(CenterY, edge, 256-edge);
    }
}
