#nullable enable
namespace AbyssMod.Services;

/// <summary>计算裁剪图片在 Sprite 逻辑画布中的位置，不分配纹理。</summary>
internal readonly record struct ImageCanvasLayout(
    int Width,
    int Height,
    int OffsetX,
    int OffsetY,
    bool UsedSpriteOffset
)
{
    internal static bool TryCreate(
        int imageWidth,
        int imageHeight,
        int canvasWidth,
        int canvasHeight,
        (int Width, int Height, int X, int Y)? packedSprite,
        out ImageCanvasLayout layout
    )
    {
        layout = default;
        if (
            canvasWidth <= 0
            || canvasHeight <= 0
            || imageWidth <= 0
            || imageHeight <= 0
            || imageWidth > canvasWidth
            || imageHeight > canvasHeight
            || imageWidth == canvasWidth && imageHeight == canvasHeight
        )
            return false;

        if (
            packedSprite is { } packed
            && packed.Width == imageWidth
            && packed.Height == imageHeight
            && Fits(packed.X, packed.Y, imageWidth, imageHeight, canvasWidth, canvasHeight)
        )
        {
            layout = new(canvasWidth, canvasHeight, packed.X, packed.Y, true);
            return true;
        }
        layout = new(
            canvasWidth,
            canvasHeight,
            (canvasWidth - imageWidth) / 2,
            (canvasHeight - imageHeight) / 2,
            false
        );
        return true;
    }

    private static bool Fits(
        int x,
        int y,
        int width,
        int height,
        int canvasWidth,
        int canvasHeight
    ) => x >= 0 && y >= 0 && (long)x + width <= canvasWidth && (long)y + height <= canvasHeight;
}
