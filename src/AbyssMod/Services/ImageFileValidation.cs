#nullable enable
using System;
using System.Buffers.Binary;
using System.IO;
using static AbyssMod.Services.ImageReplacementLimits;

namespace AbyssMod.Services;

/// <summary>解码前检查 PNG/JPEG 尺寸，避免为超大或损坏的图片分配 Unity 纹理。</summary>
internal static class ImageFileValidation
{
    internal static void Validate(ReadOnlySpan<byte> bytes, string extension)
    {
        if (string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase))
            ValidatePng(bytes);
        else
            ValidateJpeg(bytes);
    }

    internal static void ValidateDimensions(uint width, uint height, string format)
    {
        if (
            width == 0
            || height == 0
            || width > MaxImageDimension
            || height > MaxImageDimension
            || (long)width * height > MaxImagePixels
        )
            throw new InvalidDataException(
                $"{format} dimensions {width}x{height} exceed the supported limit"
            );
    }

    private static void ValidatePng(ReadOnlySpan<byte> bytes)
    {
        ReadOnlySpan<byte> signature = stackalloc byte[]
        {
            0x89,
            0x50,
            0x4E,
            0x47,
            0x0D,
            0x0A,
            0x1A,
            0x0A,
        };
        if (
            bytes.Length < 24
            || !bytes[..signature.Length].SequenceEqual(signature)
            || BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(8, 4)) != 13
            || bytes[12] != (byte)'I'
            || bytes[13] != (byte)'H'
            || bytes[14] != (byte)'D'
            || bytes[15] != (byte)'R'
        )
            throw new InvalidDataException("file does not contain a valid PNG header");
        ValidateDimensions(
            BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(16, 4)),
            BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(20, 4)),
            "PNG"
        );
    }

    private static void ValidateJpeg(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 4 || bytes[0] != 0xFF || bytes[1] != 0xD8)
            throw new InvalidDataException("file does not contain a valid JPEG header");
        int offset = 2;
        while (offset < bytes.Length)
        {
            if (bytes[offset] != 0xFF)
                throw new InvalidDataException("JPEG marker is missing its 0xFF prefix");
            while (offset < bytes.Length && bytes[offset] == 0xFF)
                offset++;
            if (offset >= bytes.Length)
                break;
            byte marker = bytes[offset++];
            if (marker == 0xD9 || marker == 0xDA)
                break;
            if (marker == 0x00 || marker == 0xD8)
                throw new InvalidDataException("JPEG contains an invalid marker");
            if (marker == 0x01 || marker >= 0xD0 && marker <= 0xD7)
                continue;
            if (offset + 2 > bytes.Length)
                break;
            int length = bytes[offset] << 8 | bytes[offset + 1];
            if (length < 2 || offset + length > bytes.Length)
                throw new InvalidDataException("JPEG contains an invalid segment length");
            bool isFrame =
                marker >= 0xC0
                && marker <= 0xCF
                && marker != 0xC4
                && marker != 0xC8
                && marker != 0xCC;
            if (isFrame)
            {
                if (length < 11)
                    throw new InvalidDataException("JPEG frame header is incomplete");
                int components = bytes[offset + 7];
                if (components <= 0 || length != 8 + 3 * components)
                    throw new InvalidDataException("JPEG frame header has an invalid length");
                uint height = (uint)(bytes[offset + 3] << 8 | bytes[offset + 4]);
                uint width = (uint)(bytes[offset + 5] << 8 | bytes[offset + 6]);
                ValidateDimensions(width, height, "JPEG");
                return;
            }
            offset += length;
        }
        throw new InvalidDataException("JPEG dimensions could not be read");
    }
}
