using Microsoft.AspNetCore.Identity;

namespace PulsoIO.Modules.Identity.Domain;

public sealed class User : IdentityUser<Guid>
{
    private User()
    {
    }

    public User(string name, string email, bool isActive = true, Guid? clientId = null)
    {
        Id = Guid.NewGuid();
        Name = name.Trim();
        Email = email.Trim();
        UserName = Email;
        IsActive = isActive;
        ClientId = clientId;
        LockoutEnabled = true;
        SecurityStamp = Guid.NewGuid().ToString("N");
    }

    public string Name { get; private set; } = string.Empty;

    public bool IsActive { get; private set; } = true;

    public Guid? ClientId { get; private set; }

    public byte[]? ProfilePhoto { get; private set; }

    public string? ProfilePhotoContentType { get; private set; }

    public void Rename(string name)
    {
        Name = name.Trim();
    }

    public bool SetActiveStatus(bool isActive)
    {
        if (IsActive == isActive)
        {
            return false;
        }

        IsActive = isActive;
        return true;
    }

    public void SetProfilePhoto(byte[] photo, string contentType)
    {
        ArgumentNullException.ThrowIfNull(photo);

        ProfilePhoto = photo.ToArray();
        ProfilePhotoContentType = contentType;
    }

    public void RemoveProfilePhoto()
    {
        ProfilePhoto = null;
        ProfilePhotoContentType = null;
    }
}
