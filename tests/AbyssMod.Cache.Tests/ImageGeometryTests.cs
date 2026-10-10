using System;
using System.Buffers.Binary;
using System.IO;
using AbyssMod.Services;
using Xunit;

namespace CacheTests;

public sealed class ImageGeometryTests
{
    [Fact]
    public void HeaderChecksAcceptPngAndJpegAndRejectTruncation()
    {
        var png = PngHeader(512, 256);
        ImageFileValidation.Validate(png, ".PNG");
        Assert.Throws<InvalidDataException>(() => ImageFileValidation.Validate(png[..20], ".png"));
        png[12] = (byte)'X';
        Assert.Throws<InvalidDataException>(() => ImageFileValidation.Validate(png, ".png"));

        byte[] jpeg =
        {
            0xff,
            0xd8,
            0xff,
            0xc0,
            0,
            17,
            8,
            1,
            0,
            2,
            0,
            3,
            1,
            0x11,
            0,
            2,
            0x11,
            0,
            3,
            0x11,
            0,
        };
        ImageFileValidation.Validate(jpeg, ".jpg");
        Assert.Throws<InvalidDataException>(() => ImageFileValidation.Validate(jpeg[..10], ".jpg"));
        jpeg[11] = 0;
        Assert.Throws<InvalidDataException>(() => ImageFileValidation.Validate(jpeg, ".jpg"));
    }

    [Theory]
    [InlineData(0u, 100u)]
    [InlineData(100u, 0u)]
    [InlineData(8193u, 1u)]
    [InlineData(5000u, 5000u)]
    public void OversizedOrEmptyImagesAreRejectedBeforeDecoding(uint width, uint height) =>
        Assert.Throws<InvalidDataException>(() =>
            ImageFileValidation.Validate(PngHeader(width, height), ".png")
        );

    [Fact]
    public void CanvasUsesPackedOffsetAndFallsBackToCenterWhenMetadataDoesNotFit()
    {
        Assert.True(ImageCanvasLayout.TryCreate(20, 10, 50, 40, (20, 10, 3, 7), out var packed));
        Assert.Equal(new ImageCanvasLayout(50, 40, 3, 7, true), packed);
        Assert.True(ImageCanvasLayout.TryCreate(20, 10, 51, 41, (20, 10, 40, 7), out var centered));
        Assert.Equal(new ImageCanvasLayout(51, 41, 15, 15, false), centered);
        Assert.True(ImageCanvasLayout.TryCreate(20, 10, 51, 41, null, out var withoutMetadata));
        Assert.Equal(centered, withoutMetadata);
    }

    [Theory]
    [InlineData(20, 10, 20, 10)]
    [InlineData(21, 10, 20, 10)]
    [InlineData(20, 11, 20, 10)]
    [InlineData(0, 10, 20, 20)]
    [InlineData(20, 10, 0, 20)]
    public void EqualOversizedOrInvalidImagesDoNotAllocateLogicalCanvases(
        int width,
        int height,
        int canvasWidth,
        int canvasHeight
    ) =>
        Assert.False(
            ImageCanvasLayout.TryCreate(width, height, canvasWidth, canvasHeight, null, out _)
        );

    private static byte[] PngHeader(uint width, uint height)
    {
        byte[] bytes = new byte[24];
        new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }.CopyTo(bytes, 0);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(8, 4), 13);
        "IHDR"u8.CopyTo(bytes.AsSpan(12));
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(16, 4), width);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(20, 4), height);
        return bytes;
    }
}
