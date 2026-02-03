using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Switchback.Core.Repositories;
using Switchback.Core.Services;

namespace Switchback.Web.Pages;

public class LoginModel : PageModel
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;

    public LoginModel(IUserRepository users, IPasswordHasher hasher)
    {
        _users = users;
        _hasher = hasher;
    }

    [BindProperty]
    [Required]
    [Display(Name = "Username")]
    public string Username { get; set; } = "";

    [BindProperty]
    [Required]
    [Display(Name = "Password")]
    public string Password { get; set; } = "";

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var normalized = Username.Trim().ToLowerInvariant();
        var user = await _users.GetByUsernameAsync(normalized);
        if (user == null || !_hasher.VerifyPassword(Password, user.PasswordHash))
        {
            ModelState.AddModelError(string.Empty, "Invalid username or password.");
            return Page();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId),
            new(ClaimTypes.Name, normalized)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
        return RedirectToPage("/Connections");
    }
}
