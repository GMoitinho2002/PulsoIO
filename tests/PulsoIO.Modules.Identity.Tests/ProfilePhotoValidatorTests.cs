using PulsoIO.Modules.Identity.Authentication;
using Xunit;

namespace PulsoIO.Modules.Identity.Tests;

public sealed class ProfilePhotoValidatorTests
{
    [Theory]
    [InlineData("image/jpeg", new byte[] { 0xFF, 0xD8, 0xFF, 0x00 })]
    [InlineData("image/png", new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })]
    [InlineData("image/webp", new byte[] { 0x52, 0x49, 0x46, 0x46, 0, 0, 0, 0, 0x57, 0x45, 0x42, 0x50 })]
    public void AcceptsSupportedContentWhenMagicBytesMatch(string contentType, byte[] content)
    {
        Assert.True(ProfilePhotoValidator.IsSupported(contentType, content));
    }

    [Theory]
    [InlineData("image/jpeg", new byte[] { 0x89, 0x50, 0x4E, 0x47 })]
    [InlineData("image/png", new byte[] { 0xFF, 0xD8, 0xFF })]
    [InlineData("image/svg+xml", new byte[] { 0x3C, 0x73, 0x76, 0x67 })]
    [InlineData("image/webp", new byte[] { 0x52, 0x49, 0x46, 0x46 })]
    public void RejectsUnsupportedOrSpoofedContent(string contentType, byte[] content)
    {
        Assert.False(ProfilePhotoValidator.IsSupported(contentType, content));
    }

    [Fact]
    public void PhotoLimitIsExactlyTwoMebibytes()
    {
        Assert.Equal(2 * 1024 * 1024, ProfilePhotoValidator.MaximumSizeBytes);
    }
}
