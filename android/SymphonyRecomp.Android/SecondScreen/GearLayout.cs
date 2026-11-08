namespace SymphonyRecomp.Android.SecondScreen;

public readonly record struct GearBounds(float Left,float Top,float Right,float Bottom)
{
    public float CenterX => (Left+Right)/2;
    public float CenterY => (Top+Bottom)/2;
    public bool Contains(float x,float y) => x>=Left && x<=Right && y>=Top && y<=Bottom;
}

// Shared design-space geometry. Text callers supply actual paint/typeface ink bounds.
public static class GearLayout
{
    public static readonly GearBounds Card = new(42,200,958,798);
    public static GearBounds Tab(int index,int count)
    {
        if(count<1 || index<0 || index>=count)throw new ArgumentOutOfRangeException(nameof(index));
        float width=(Card.Right-Card.Left-12*(count-1))/count;
        float left=Card.Left+index*(width+12);
        return new(left,122,left+width,182);
    }
    public static float InkOrigin(float center,float inkStart,float inkEnd) => center-(inkStart+inkEnd)/2;
    public static float EmptyFirstBaseline(float titleInkTop,float lastLineInkBottom) =>
        InkOrigin(Card.CenterY,titleInkTop,86+lastLineInkBottom);
}
