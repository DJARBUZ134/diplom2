using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace LinuxTrainer.Client.Auth;

public class PersistentAuthenticationStateProvider : AuthenticationStateProvider
{
    private static readonly Task<AuthenticationState> Unauthenticated =
        Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));

    private readonly Task<AuthenticationState> _authenticationStateTask = Unauthenticated;

    public PersistentAuthenticationStateProvider(PersistentComponentState state)
    {
        if (!state.TryTakeFromJson<UserInfo>(UserInfo.PersistKey, out var info) || info is null)
            return;

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, info.UserId.ToString()),
            new Claim(ClaimTypes.Name, info.Login),
            new Claim("display_name", info.DisplayName),
            new Claim(ClaimTypes.Role, info.Role)
        };
        var identity = new ClaimsIdentity(claims, authenticationType: "PersistedCookie");
        _authenticationStateTask = Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity)));
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync() => _authenticationStateTask;
}
