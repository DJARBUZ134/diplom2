using System.Security.Claims;
using LinuxTrainer.Client.Auth;
using LinuxTrainer.Data;
using LinuxTrainer.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LinuxTrainer.Services;

public record PendingAvatar(int UserId, string Login, string DisplayName, string AvatarBase64);

public interface IUserService
{
    Task<User?> ValidateAsync(string login, string password);
    ClaimsPrincipal BuildPrincipal(User user, string scheme);
    UserInfo ToUserInfo(User user);
    Task<UserInfo?> GetInfoAsync(int userId);
    Task<List<User>> ListAsync();
    Task<User> CreateAsync(string login, string password, string role, string displayName);
    Task DeleteAsync(int userId);
    Task<bool> SetRoleAsync(int userId, string role);
    Task<bool> ResetPasswordAsync(int userId, string newPassword);
    Task<bool> SetPendingAvatarAsync(int userId, string base64);
    Task<List<PendingAvatar>> ListPendingAvatarsAsync();
    Task<bool> ApproveAvatarAsync(int userId);
    Task<bool> RejectAvatarAsync(int userId);
}

public class UserService : IUserService
{
    private readonly AppDbContext _db;
    private readonly PasswordHasher<User> _hasher = new();

    public UserService(AppDbContext db) => _db = db;

    public async Task<User?> ValidateAsync(string login, string password)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Login == login);
        if (user is null) return null;

        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (result == PasswordVerificationResult.Failed) return null;

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _hasher.HashPassword(user, password);
            await _db.SaveChangesAsync();
        }
        return user;
    }

    public ClaimsPrincipal BuildPrincipal(User user, string scheme)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Login),
            new Claim("display_name", user.DisplayName),
            new Claim(ClaimTypes.Role, user.Role)
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, scheme));
    }

    public UserInfo ToUserInfo(User user) => new()
    {
        UserId = user.Id,
        Login = user.Login,
        DisplayName = user.DisplayName,
        Role = user.Role,
        AvatarBase64 = user.AvatarApprovedBase64
    };

    public async Task<UserInfo?> GetInfoAsync(int userId)
    {
        var user = await _db.Users.FindAsync(userId);
        return user is null ? null : ToUserInfo(user);
    }

    public Task<List<User>> ListAsync() =>
        _db.Users.OrderBy(u => u.Id).ToListAsync();

    public async Task<User> CreateAsync(string login, string password, string role, string displayName)
    {
        var user = new User { Login = login, Role = role, DisplayName = displayName };
        user.PasswordHash = _hasher.HashPassword(user, password);
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    public async Task DeleteAsync(int userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user is null) return;
        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
    }

    public async Task<bool> SetRoleAsync(int userId, string role)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user is null) return false;
        user.Role = role;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ResetPasswordAsync(int userId, string newPassword)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user is null) return false;
        user.PasswordHash = _hasher.HashPassword(user, newPassword);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SetPendingAvatarAsync(int userId, string base64)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user is null) return false;
        user.AvatarPendingBase64 = base64;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<List<PendingAvatar>> ListPendingAvatarsAsync()
    {
        return await _db.Users
            .Where(u => u.AvatarPendingBase64 != null)
            .Select(u => new PendingAvatar(u.Id, u.Login, u.DisplayName, u.AvatarPendingBase64!))
            .ToListAsync();
    }

    public async Task<bool> ApproveAvatarAsync(int userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user is null || user.AvatarPendingBase64 is null) return false;
        user.AvatarApprovedBase64 = user.AvatarPendingBase64;
        user.AvatarPendingBase64 = null;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RejectAvatarAsync(int userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user is null) return false;
        user.AvatarPendingBase64 = null;
        await _db.SaveChangesAsync();
        return true;
    }
}
