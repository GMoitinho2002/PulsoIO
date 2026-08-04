namespace PulsoIO.Modules.Identity.Authentication;

internal static class ProfilePhotoValidator
{
    public const int MaximumSizeBytes = 2 * 1024 * 1024;

    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] RiffSignature = "RIFF"u8.ToArray();
    private static readonly byte[] WebpSignature = "WEBP"u8.ToArray();

    public static bool IsSupported(string? contentType, ReadOnlySpan<byte> content)
    {
        return contentType switch
        {
            "image/jpeg" => content.StartsWith(JpegSignature),
            "image/png" => content.StartsWith(PngSignature),
            "image/webp" => content.Length >= 12 &&
                content[..4].SequenceEqual(RiffSignature) &&
                content.Slice(8, 4).SequenceEqual(WebpSignature),
            _ => false
        };
    }
}
