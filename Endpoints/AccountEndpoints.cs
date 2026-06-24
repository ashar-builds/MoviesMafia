using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MoviesMafia.Domain;
using MoviesMafia.Domain.Entities;
using MoviesMafia.Services.Email;
using MoviesMafia.Services.Storage;

namespace MoviesMafia.Endpoints;

/// <summary>
/// Form-post endpoints for cookie-mutating account actions. These must run on a real HTTP request
/// (not inside a Blazor render or ReactiveBlazor dispatch), so they live here rather than in components.
/// Razor pages render the forms (with antiforgery tokens) that post to these routes.
/// </summary>
public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/account");

        group.MapPost("/login", LoginAsync).AllowAnonymous();
        group.MapPost("/logout", LogoutAsync);
        group.MapPost("/register", RegisterAsync).AllowAnonymous();
        group.MapPost("/avatar", UpdateAvatarAsync);

        return app;
    }

    private static async Task<IResult> LoginAsync(
        HttpContext http,
        SignInManager<AppUser> signInManager,
        [FromForm] string username,
        [FromForm] string password,
        [FromForm] string? returnUrl)
    {
        var result = await signInManager.PasswordSignInAsync(username, password, isPersistent: true, lockoutOnFailure: false);

        if (result.Succeeded)
        {
            return Results.LocalRedirect(SafeReturnUrl(returnUrl));
        }

        var reason = result.IsNotAllowed
            ? "Please confirm your email address before signing in."
            : "Invalid username or password.";
        return Results.LocalRedirect($"/account/login?error={Uri.EscapeDataString(reason)}");
    }

    private static async Task<IResult> LogoutAsync(SignInManager<AppUser> signInManager)
    {
        await signInManager.SignOutAsync();
        return Results.LocalRedirect("/");
    }

    private static async Task<IResult> RegisterAsync(
        HttpContext http,
        UserManager<AppUser> userManager,
        IEmailSender emailSender,
        IProfilePictureStore avatars,
        IWebHostEnvironment env,
        ILoggerFactory loggerFactory,
        [FromForm] RegisterForm form,
        IFormFile? profilePicture)
    {
        var logger = loggerFactory.CreateLogger("AccountEndpoints.Register");

        var validation = Validate(form);
        if (validation is not null)
        {
            return RedirectToSignupWithError(validation);
        }

        if (await userManager.FindByEmailAsync(form.Email) is not null)
        {
            return RedirectToSignupWithError("An account with that email already exists.");
        }

        var user = new AppUser
        {
            UserName = form.Username,
            Email = form.Email,
            // Auto-confirm in Development so login works without a live SMTP server.
            EmailConfirmed = env.IsDevelopment(),
        };

        if (profilePicture is { Length: > 0 })
        {
            try
            {
                await using var stream = profilePicture.OpenReadStream();
                user.ProfilePictureFileName = await avatars.SaveAsync(form.Username, stream, profilePicture.FileName);
            }
            catch (InvalidOperationException ex)
            {
                return RedirectToSignupWithError(ex.Message);
            }
        }

        var created = await userManager.CreateAsync(user, form.Password);
        if (!created.Succeeded)
        {
            var message = string.Join(" ", created.Errors.Select(e => e.Description));
            return RedirectToSignupWithError(message);
        }

        await userManager.AddToRoleAsync(user, Roles.User);

        if (env.IsDevelopment())
        {
            return Results.LocalRedirect("/account/login?message=" +
                Uri.EscapeDataString("Account created. You can sign in now."));
        }

        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var verifyUrl = $"{http.Request.Scheme}://{http.Request.Host}/account/verify-email" +
            $"?email={Uri.EscapeDataString(form.Email)}&token={Uri.EscapeDataString(token)}";

        try
        {
            await emailSender.SendAsync(form.Email, "Verify your MoviesMafia email",
                $"<p>Welcome to MoviesMafia! Please confirm your email by clicking " +
                $"<a href=\"{verifyUrl}\">this link</a>.</p>");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send verification email to {Email}", form.Email);
        }

        return Results.LocalRedirect("/account/login?message=" +
            Uri.EscapeDataString("Check your email for a confirmation link to activate your account."));
    }

    private static async Task<IResult> UpdateAvatarAsync(
        HttpContext http,
        UserManager<AppUser> userManager,
        IProfilePictureStore avatars,
        IFormFile? profilePicture)
    {
        var user = await userManager.GetUserAsync(http.User);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        if (profilePicture is not { Length: > 0 })
        {
            return Results.LocalRedirect("/account/profile?error=" + Uri.EscapeDataString("Please choose an image."));
        }

        try
        {
            await using var stream = profilePicture.OpenReadStream();
            user.ProfilePictureFileName = await avatars.SaveAsync(user.UserName!, stream, profilePicture.FileName);
            await userManager.UpdateAsync(user);
        }
        catch (InvalidOperationException ex)
        {
            return Results.LocalRedirect("/account/profile?error=" + Uri.EscapeDataString(ex.Message));
        }

        return Results.LocalRedirect("/account/profile?message=" + Uri.EscapeDataString("Profile picture updated."));
    }

    private static string? Validate(RegisterForm form)
    {
        if (string.IsNullOrWhiteSpace(form.Username) || form.Username.Length > 20)
        {
            return "Username is required and must be 20 characters or fewer.";
        }
        if (string.IsNullOrWhiteSpace(form.Email) || !new EmailAddressAttribute().IsValid(form.Email))
        {
            return "A valid email address is required.";
        }
        if (string.IsNullOrWhiteSpace(form.Password) || form.Password.Length < 6)
        {
            return "Password must be at least 6 characters.";
        }
        if (form.Password != form.ConfirmPassword)
        {
            return "Passwords do not match.";
        }
        return null;
    }

    private static IResult RedirectToSignupWithError(string message) =>
        Results.LocalRedirect("/account/signup?error=" + Uri.EscapeDataString(message));

    private static string SafeReturnUrl(string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl) && returnUrl.StartsWith('/') ? returnUrl : "/movies";

    public sealed class RegisterForm
    {
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
