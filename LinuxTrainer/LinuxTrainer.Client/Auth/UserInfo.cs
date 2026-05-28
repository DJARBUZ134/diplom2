namespace LinuxTrainer.Client.Auth;

public class UserInfo
{
    public int UserId { get; set; }
    public string Login { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Role { get; set; } = "";
    public string? AvatarBase64 { get; set; }

    public const string PersistKey = "userInfo";
}
