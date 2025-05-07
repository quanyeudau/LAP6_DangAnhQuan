using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ASC.Web.Areas.Accounts.Models;
using ASC.Web.Services;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace ASC.Web.Areas.Accounts.Controllers;

[Authorize]
[Area("Accounts")]
public class AccountController : Controller
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IEmailSender _emailSender;
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        UserManager<IdentityUser> userManager,
        IEmailSender emailSender,
        SignInManager<IdentityUser> signInManager,
        ILogger<AccountController> logger)
    {
        _userManager = userManager;
        _emailSender = emailSender;
        _signInManager = signInManager;
        _logger = logger;
    }

    // GET: /Accounts/Index
    public IActionResult Index()
    {
        _logger.LogInformation("Index action called.");
        return View();
    }

    // GET: /Accounts/ServiceEngineers
    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> ServiceEngineers()
    {
        _logger.LogInformation("ServiceEngineers GET action called.");
        try
        {
            var engineers = await _userManager.GetUsersInRoleAsync(Roles.Engineer.ToString());
            _logger.LogInformation($"Found {engineers.Count} service engineers.");
            if (engineers.Any())
            {
                foreach (var engineer in engineers)
                {
                    _logger.LogInformation($"Engineer: Email={engineer.Email}, UserName={engineer.UserName}");
                }
            }
            else
            {
                _logger.LogWarning("No service engineers found. Ensure users with Role=Engineer exist in the database.");
            }

            // Lưu danh sách vào session để sử dụng trong POST
            HttpContext.Session.SetSession("ServiceEngineers", engineers);

            return View(new ServiceEngineerViewModel
            {
                ServiceEngineers = engineers.ToList(),
                Registration = new ServiceEngineerRegistrationViewModel { IsEdit = false }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ServiceEngineers GET action.");
            return StatusCode(500, new { success = false, message = "Internal server error" });
        }
    }

    // POST: /Accounts/ServiceEngineers
    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ServiceEngineers(ServiceEngineerViewModel model)
    {
        _logger.LogInformation("ServiceEngineers POST action called.");
        try
        {
            // Lấy danh sách từ session
            model.ServiceEngineers = HttpContext.Session.GetSession<List<IdentityUser>>("ServiceEngineers");

            // Kiểm tra model state
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Model state invalid in ServiceEngineers POST.");
                return View(model);
            }

            var reg = model.Registration;

            // Trường hợp chỉnh sửa tài khoản
            if (reg.IsEdit)
            {
                var user = await _userManager.FindByEmailAsync(reg.Email);
                if (user == null)
                {
                    _logger.LogWarning($"User with email {reg.Email} not found.");
                    ModelState.AddModelError("", "User not found.");
                    return View(model);
                }

                // Cập nhật thông tin
                user.UserName = reg.UserName;
                user.NormalizedUserName = _userManager.NormalizeName(reg.UserName);

                var updateResult = await _userManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                {
                    _logger.LogError($"Failed to update user {reg.Email}: {string.Join(", ", updateResult.Errors.Select(e => e.Description))}");
                    AddErrors(updateResult);
                    return View(model);
                }

                // Cập nhật mật khẩu nếu có
                if (!string.IsNullOrEmpty(reg.Password))
                {
                    var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                    var passwordResult = await _userManager.ResetPasswordAsync(user, token, reg.Password);
                    if (!passwordResult.Succeeded)
                    {
                        _logger.LogError($"Failed to reset password for {reg.Email}: {string.Join(", ", passwordResult.Errors.Select(e => e.Description))}");
                        AddErrors(passwordResult);
                        return View(model);
                    }
                }

                // Cập nhật trạng thái IsActive
                await UpdateIsActiveClaim(user, reg.IsActive);

                // Gửi email thông báo
                if (!string.IsNullOrEmpty(user.Email))
                {
                    await SendAccountEmail(user.Email, reg.IsActive, "updated", reg.Password);
                }

                return RedirectToAction(nameof(ServiceEngineers));
            }
            // Trường hợp tạo mới tài khoản
            else
            {
                // Kiểm tra password bắt buộc khi tạo mới
                if (string.IsNullOrEmpty(reg.Password))
                {
                    _logger.LogWarning("Password is required when creating a new engineer.");
                    return Json(new { success = false, message = "Password is required." });
                }

                var user = new IdentityUser
                {
                    UserName = reg.UserName,
                    Email = reg.Email,
                    EmailConfirmed = true
                };

                // Tạo tài khoản
                var createResult = await _userManager.CreateAsync(user, reg.Password);
                if (!createResult.Succeeded)
                {
                    _logger.LogError($"Failed to create user {reg.Email}: {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
                    return Json(new { success = false, errors = createResult.Errors.Select(e => e.Description) });
                }

                // Gán vai trò và claims
                await _userManager.AddToRoleAsync(user, Roles.Engineer.ToString());
                await _userManager.AddClaimAsync(user, new Claim(ClaimTypes.Email, reg.Email));
                await _userManager.AddClaimAsync(user, new Claim("IsActive", reg.IsActive.ToString()));

                // Gửi email thông báo
                if (!string.IsNullOrEmpty(user.Email))
                {
                    try
                    {
                        await SendAccountEmail(user.Email, reg.IsActive, "created", reg.Password);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Failed to send email to {user.Email} after creating account.");
                        return Json(new { success = false, message = "Account created, but failed to send email." });
                    }
                }

                // Trả về JSON để cập nhật danh sách qua AJAX
                return Json(new
                {
                    success = true,
                    engineer = new
                    {
                        email = user.Email,
                        userName = user.UserName,
                        isActive = reg.IsActive
                    }
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ServiceEngineers POST action.");
            return Json(new { success = false, message = "Internal server error" });
        }
    }

    // GET: /Accounts/Customers
    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> Customers()
    {
        _logger.LogInformation("Customers GET action called.");
        try
        {
            // Lấy tất cả tài khoản từ bảng AspNetUsers
            var allUsers = await _userManager.Users.ToListAsync();
            _logger.LogInformation($"Found {allUsers.Count} users in AspNetUsers.");
            if (!allUsers.Any())
            {
                _logger.LogWarning("No users found in the database.");
            }
            else
            {
                foreach (var user in allUsers)
                {
                    _logger.LogInformation($"User: Id={user.Id}, Email={user.Email}, UserName={user.UserName}");
                    var claims = await _userManager.GetClaimsAsync(user);
                    _logger.LogInformation($"Claims for {user.Email}: {string.Join(", ", claims.Select(c => $"{c.Type}:{c.Value}"))}");
                }
            }

            return View(new CustomerViewModel
            {
                Customers = allUsers, // Hiển thị tất cả tài khoản
                Registration = new CustomerRegistrationViewModel { IsEdit = false }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Customers GET action.");
            return StatusCode(500, new { success = false, message = "Internal server error" });
        }
    }

    // POST: /Accounts/Customers
    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Customers(CustomerViewModel model)
    {
        _logger.LogInformation("Customers POST action called.");
        try
        {
            // Xóa validation không cần thiết khi chỉnh sửa
            if (model.Registration.IsEdit)
            {
                ModelState.Remove("Registration.UserName");
                ModelState.Remove("Registration.Password");
                ModelState.Remove("Registration.ConfirmPassword");
            }

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Model state invalid in Customers POST.");
                var allUsers = await _userManager.Users.ToListAsync();
                model.Customers = allUsers;
                return View(model);
            }

            var reg = model.Registration;
            if (reg.IsEdit)
            {
                var user = await _userManager.FindByEmailAsync(reg.Email);
                if (user == null)
                {
                    _logger.LogWarning($"User with email {reg.Email} not found.");
                    ModelState.AddModelError("", "User not found.");
                    var allUsers = await _userManager.Users.ToListAsync();
                    model.Customers = allUsers;
                    return View(model);
                }

                // Cập nhật trạng thái IsActive
                await UpdateIsActiveClaim(user, reg.IsActive);

                // Gửi email thông báo
                if (!string.IsNullOrEmpty(user.Email))
                {
                    await SendAccountEmail(user.Email, reg.IsActive, "modified");
                }
            }

            return RedirectToAction(nameof(Customers));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Customers POST action.");
            return StatusCode(500, new { success = false, message = "Internal server error" });
        }
    }

    // GET: /Accounts/Profile
    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        _logger.LogInformation("Profile GET action called.");
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("User ID not found in claims, redirecting to Login.");
                return RedirectToAction("Login", "Account");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                _logger.LogWarning($"User with ID {userId} not found, redirecting to Login.");
                return RedirectToAction("Login", "Account");
            }

            var claims = await _userManager.GetClaimsAsync(user);
            var isActiveClaim = claims.FirstOrDefault(c => c.Type == "IsActive");

            bool isActive = false;
            if (isActiveClaim != null)
            {
                bool.TryParse(isActiveClaim.Value, out isActive);
            }

            return View(new ProfileModel
            {
                UserName = user.UserName ?? string.Empty,
                IsActive = isActive
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Profile GET action.");
            return StatusCode(500, new { success = false, message = "Internal server error" });
        }
    }

    // POST: /Accounts/Profile
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(ProfileModel profile)
    {
        _logger.LogInformation("Profile POST action called.");
        try
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Model state invalid in Profile POST.");
                return Json(new { success = false, message = "Invalid data." });
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("User ID not found in claims.");
                return Json(new { success = false, message = "User not authenticated." });
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                _logger.LogWarning($"User with ID {userId} not found.");
                return Json(new { success = false, message = "User not found." });
            }

            // Kiểm tra username đã tồn tại chưa
            var existingUser = await _userManager.FindByNameAsync(profile.UserName);
            if (existingUser != null && existingUser.Id != user.Id)
            {
                _logger.LogWarning($"Username {profile.UserName} is already taken.");
                return Json(new { success = false, message = "This username is already taken." });
            }

            // Cập nhật thông tin
            user.UserName = profile.UserName;
            user.NormalizedUserName = _userManager.NormalizeName(profile.UserName);

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                _logger.LogError($"Failed to update profile for user {userId}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                return Json(new { success = false, message = "Failed to update profile: " + string.Join(", ", result.Errors.Select(e => e.Description)) });
            }

            await _signInManager.RefreshSignInAsync(user);
            _logger.LogInformation($"Profile updated successfully for user {userId}.");
            return Json(new { success = true, message = "Profile updated successfully.", userName = user.UserName });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Profile POST action.");
            return Json(new { success = false, message = "Internal server error" });
        }
    }

    // GET: /Accounts/Login
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string returnUrl = null)
    {
        _logger.LogInformation("Login GET action called.");
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    // POST: /Accounts/ExternalLogin
    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> ExternalLogin(string provider, string returnUrl = null)
    {
        _logger.LogInformation($"ExternalLogin POST action called with provider: {provider}.");
        try
        {
            var redirectUrl = Url.Action("ExternalLoginCallback", "Account", new { returnUrl });
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
            return Challenge(properties, provider);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ExternalLogin POST action.");
            return StatusCode(500, new { success = false, message = "Internal server error" });
        }
    }

    // GET: /Accounts/ExternalLoginCallback
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> ExternalLoginCallback(string returnUrl = null, string remoteError = null)
    {
        _logger.LogInformation("ExternalLoginCallback action called.");
        try
        {
            if (remoteError != null)
            {
                _logger.LogError($"Error from external provider: {remoteError}");
                ModelState.AddModelError("", $"Error from external provider: {remoteError}");
                return RedirectToAction("Login", "Account");
            }

            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                _logger.LogError("External login info is null.");
                return RedirectToAction("Login", "Account");
            }

            // Kiểm tra nếu tài khoản đã đăng nhập bằng Google trước đó
            var result = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);
            if (result.Succeeded)
            {
                _logger.LogInformation("User logged in with external provider.");
                return RedirectToAction("Customers");
            }

            // Lấy thông tin từ Google
            var email = info.Principal.FindFirstValue(ClaimTypes.Email);
            var userName = info.Principal.FindFirstValue(ClaimTypes.Name);
            if (string.IsNullOrEmpty(userName))
            {
                userName = email?.Split('@')[0] ?? "UnknownUser";
                _logger.LogWarning($"UserName not provided by Google, using default: {userName}");
            }

            // Tạo tài khoản mới
            var user = new IdentityUser
            {
                UserName = userName,
                Email = email,
                EmailConfirmed = true
            };

            var createResult = await _userManager.CreateAsync(user);
            if (!createResult.Succeeded)
            {
                _logger.LogError($"Failed to create user {email}: {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
                AddErrors(createResult);
                return RedirectToAction("Login", "Account");
            }

            // Liên kết tài khoản Google
            var addLoginResult = await _userManager.AddLoginAsync(user, info);
            if (!addLoginResult.Succeeded)
            {
                _logger.LogError($"Failed to add login for {email}: {string.Join(", ", addLoginResult.Errors.Select(e => e.Description))}");
                await _userManager.DeleteAsync(user);
                AddErrors(addLoginResult);
                return RedirectToAction("Login", "Account");
            }

            // Thêm claims cho khách hàng
            await _userManager.AddClaimAsync(user, new Claim("UserType", "Customer"));
            await _userManager.AddClaimAsync(user, new Claim("IsActive", "True"));

            var claims = await _userManager.GetClaimsAsync(user);
            _logger.LogInformation($"Claims added for user {email}: {string.Join(", ", claims.Select(c => $"{c.Type}:{c.Value}"))}");

            // Đăng nhập tài khoản
            await _signInManager.SignInAsync(user, isPersistent: false);
            _logger.LogInformation($"User {email} created and signed in successfully.");

            return RedirectToAction("Customers");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ExternalLoginCallback action.");
            return StatusCode(500, new { success = false, message = "Internal server error" });
        }
    }

    // POST: /Accounts/ChangeUserName
    [HttpPost]
    public async Task<IActionResult> ChangeUserName(string email, string newUserName)
    {
        _logger.LogInformation($"ChangeUserName action called for email: {email}, new username: {newUserName}.");
        try
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(newUserName))
            {
                _logger.LogError("Invalid email or username provided for ChangeUserName.");
                return BadRequest(new { success = false, message = "Invalid email or username" });
            }

            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                _logger.LogError($"User with email {email} not found.");
                return BadRequest(new { success = false, message = "User not found" });
            }

            var existingUser = await _userManager.FindByNameAsync(newUserName);
            if (existingUser != null && existingUser.Id != user.Id)
            {
                _logger.LogError($"Username {newUserName} is already taken.");
                return BadRequest(new { success = false, message = "This username is already taken" });
            }

            user.UserName = newUserName;
            user.NormalizedUserName = _userManager.NormalizeName(newUserName);
            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                _logger.LogError($"Failed to update username for {email}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                return BadRequest(new { success = false, message = $"Failed to update username: {string.Join(", ", result.Errors.Select(e => e.Description))}" });
            }

            await _signInManager.RefreshSignInAsync(user);
            _logger.LogInformation($"Username for {email} updated to {newUserName}.");
            return Ok(new { success = true, message = "Username updated successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ChangeUserName action.");
            return StatusCode(500, new { success = false, message = "Internal server error" });
        }
    }

    // Gửi email thông báo
    private async Task SendAccountEmail(string email, bool isActive, string action, string password = "")
    {
        try
        {
            string subject = $"Account {action}";
            string body = isActive
                ? $"Your account has been {action}.\nEmail: {email}\nPassword: {password}"
                : "Your account has been deactivated.";

            await _emailSender.SendEmailAsync(email, subject, body);
            _logger.LogInformation($"Email sent to {email} for action: {action}.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to send email to {email}.");
            throw;
        }
    }

    // Cập nhật claim IsActive
    private async Task UpdateIsActiveClaim(IdentityUser user, bool isActive)
    {
        try
        {
            var claims = await _userManager.GetClaimsAsync(user);
            var existing = claims.FirstOrDefault(c => c.Type == "IsActive");

            if (existing != null)
            {
                await _userManager.RemoveClaimAsync(user, existing);
            }

            await _userManager.AddClaimAsync(user, new Claim("IsActive", isActive.ToString()));
            _logger.LogInformation($"IsActive claim updated for user {user.Email} to {isActive}.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to update IsActive claim for user {user.Email}.");
            throw;
        }
    }

    // Thêm lỗi vào ModelState
    private void AddErrors(IdentityResult result)
    {
        foreach (var error in result.Errors)
        {
            ModelState.AddModelError("", error.Description);
        }
    }
}

// Helper để lưu session
public static class SessionHelper
{
    public static void SetSession<T>(this ISession session, string key, T value)
    {
        var json = JsonConvert.SerializeObject(value);
        session.SetString(key, json);
    }

    public static T GetSession<T>(this ISession session, string key) where T : class, new()
    {
        var json = session.GetString(key);
        return json == null ? new T() : JsonConvert.DeserializeObject<T>(json) ?? new T();
    }
}

// Enum cho vai trò
public enum Roles
{
    Admin,
    Engineer,
    Customer
}