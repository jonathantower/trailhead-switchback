using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Switchback.Core.Entities;
using Switchback.Core.Repositories;
using Switchback.Core.Services;

namespace Switchback.Web.Pages;

public class RegisterModel : PageModel
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;

    public RegisterModel(IUserRepository users, IPasswordHasher hasher)
    {
        _users = users;
        _hasher = hasher;
    }

    [BindProperty]
    [Required, MinLength(2), MaxLength(64)]
    [Display(Name = "Username")]
    public string Username { get; set; } = "";

    [BindProperty]
    [Required, MinLength(6)]
    [Display(Name = "Password")]
    public string Password { get; set; } = "";

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var normalized = Username.Trim().ToLowerInvariant();
        var existing = await _users.GetByUsernameAsync(normalized);
        if (existing != null)
        {
            ModelState.AddModelError(nameof(Username), "Username already taken.");
            return Page();
        }

        var userId = Guid.NewGuid().ToString("N");
        var entity = new UserEntity
        {
            PartitionKey = UserEntity.PartitionKeyValue,
            RowKey = normalized,
            UserId = userId,
            PasswordHash = _hasher.HashPassword(Password),
            CreatedAt = DateTimeOffset.UtcNow
        };
        await _users.UpsertAsync(entity);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, normalized)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
        return RedirectToPage("/Connections");
    }
}
