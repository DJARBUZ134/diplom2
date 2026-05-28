using System.Security.Claims;
using LinuxTrainer.Client.Auth;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Components.Web;

namespace LinuxTrainer.Services;

internal sealed class PersistingServerAuthenticationStateProvider : ServerAuthenticationStateProvider, IDisposable
{
    private readonly PersistentComponentState _state;
    private readonly PersistingComponentStateSubscription _subscription;

    private Task<AuthenticationState>? _authenticationStateTask;

    public PersistingServerAuthenticationStateProvider(PersistentComponentState state)
    {
        _state = state;
        AuthenticationStateChanged += OnAuthenticationStateChanged;
        _subscription = state.RegisterOnPersisting(OnPersistingAsync, RenderMode.InteractiveWebAssembly);
    }

    private void OnAuthenticationStateChanged(Task<AuthenticationState> task)
    {
        _authenticationStateTask = task;
    }

    private async Task OnPersistingAsync()
    {
        var task = _authenticationStateTask ?? GetAuthenticationStateAsync();
        var authenticationState = await task;
        var principal = authenticationState.User;

        if (principal.Identity?.IsAuthenticated != true) return;

        var info = new UserInfo
        {
            UserId = int.TryParse(principal.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0,
            Login = principal.FindFirst(ClaimTypes.Name)?.Value ?? "",
            DisplayName = principal.FindFirst("display_name")?.Value ?? "",
            Role = principal.FindFirst(ClaimTypes.Role)?.Value ?? ""
        };
        _state.PersistAsJson(UserInfo.PersistKey, info);
    }

    public void Dispose()
    {
        _subscription.Dispose();
        AuthenticationStateChanged -= OnAuthenticationStateChanged;
    }
}
