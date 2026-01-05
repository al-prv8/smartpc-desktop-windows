using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SensePC.Desktop.WinUI.Services;
using System.Threading.Tasks;

namespace SensePC.Desktop.WinUI.ViewModels;

public partial class LoginViewModel : BaseViewModel
{
    private readonly ICognitoAuthService _authService;

    public LoginViewModel(ICognitoAuthService authService)
    {
        _authService = authService;
        Title = "Sign In";
    }

    [ObservableProperty]
    private string email = string.Empty;

    [ObservableProperty]
    private string password = string.Empty;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private bool showMfaDialog;

    [ObservableProperty]
    private string mfaCode = string.Empty;

    [ObservableProperty]
    private string mfaType = string.Empty;

    private string? _mfaSession;

    // Event to notify navigation
    public event System.Action? OnLoginSuccess;
    public event System.Action<string>? OnNavigateToSignUp;
    public event System.Action<string>? OnNavigateToForgotPassword;

    [RelayCommand]
    private async Task SignInAsync()
    {
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Please enter email and password";
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;

        try
        {
            var result = await _authService.SignInAsync(Email, Password);

            if (result.Success)
            {
                OnLoginSuccess?.Invoke();
            }
            else if (result.RequiresMFA)
            {
                _mfaSession = result.Session;
                MfaType = result.MFAType ?? "TOTP";
                ShowMfaDialog = true;
            }
            else if (result.RequiresEmailVerification)
            {
                ErrorMessage = "Please verify your email first. Check your inbox for verified.";
            }
            else if (result.RequiresNewPassword)
            {
                ErrorMessage = "You need to set a new password. Please use forgot password.";
            }
            else
            {
                ErrorMessage = result.Error ?? "Sign in failed";
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ConfirmMfaAsync()
    {
        if (string.IsNullOrWhiteSpace(MfaCode) || _mfaSession == null)
        {
            ErrorMessage = "Please enter the verification code";
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;

        try
        {
            AuthResult result;
            if (MfaType == "TOTP")
            {
                result = await _authService.ConfirmMFATotpAsync(MfaCode, _mfaSession, Email);
            }
            else
            {
                result = await _authService.ConfirmMFAEmailAsync(MfaCode, _mfaSession, Email);
            }

            if (result.Success)
            {
                ShowMfaDialog = false;
                OnLoginSuccess?.Invoke();
            }
            else
            {
                ErrorMessage = result.Error ?? "Verification failed";
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void NavigateToSignUp()
    {
        OnNavigateToSignUp?.Invoke(Email);
    }

    [RelayCommand]
    private void NavigateToForgotPassword()
    {
        OnNavigateToForgotPassword?.Invoke(Email);
    }

    [RelayCommand]
    private void CancelMfa()
    {
        ShowMfaDialog = false;
        MfaCode = string.Empty;
        _mfaSession = null;
    }
}
