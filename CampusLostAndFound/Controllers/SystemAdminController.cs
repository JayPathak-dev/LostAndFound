using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CampusLostAndFound.Models;
using CampusLostAndFound.Services;

namespace CampusLostAndFound.Controllers
{
    [Authorize(Roles = "SystemAdministrator")]
    public class SystemAdminController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IAuditService _auditService;

        public SystemAdminController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IAuditService auditService)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _auditService = auditService;
        }

        public async Task<IActionResult> Index()
        {
            var users = await _userManager.Users.ToListAsync();
            var roles = await _roleManager.Roles.ToListAsync();

            ViewBag.TotalUsers = users.Count;
            ViewBag.ActiveUsers = users.Count(u => u.IsActive);
            ViewBag.Roles = roles.Select(r => r.Name).ToList();

            var recentAudits = await _auditService.GetRecentLogsAsync(20);
            return View(recentAudits);
        }

        [HttpGet]
        public async Task<IActionResult> Users()
        {
            var users = await _userManager.Users.OrderByDescending(u => u.CreatedAt).ToListAsync();
            var userRoleMap = new List<UserManagementViewModel>();

            foreach (var user in users)
            {
                var userRoles = await _userManager.GetRolesAsync(user);
                userRoleMap.Add(new UserManagementViewModel
                {
                    UserId = user.Id,
                    FullName = user.FullName,
                    Email = user.Email ?? string.Empty,
                    RegistrationNumber = user.RegistrationNumber,
                    PhoneNumber = user.PhoneNumber,
                    IsActive = user.IsActive,
                    CreatedAt = user.CreatedAt,
                    CurrentRole = userRoles.FirstOrDefault() ?? "Student/Staff"
                });
            }

            ViewBag.AllRoles = new[] { "Student/Staff", "SecurityPersonnel", "CampusAdministrator", "SystemAdministrator" };
            return View(userRoleMap);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeRole(string userId, string newRole)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            var currentRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
            await _userManager.AddToRoleAsync(user, newRole);

            await _auditService.LogActionAsync(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                User.FindFirstValue(ClaimTypes.Email),
                "RoleModified",
                "ApplicationUser",
                user.Id,
                $"Changed role for {user.Email} to {newRole}.");

            TempData["SuccessMessage"] = $"Role for {user.FullName} updated to {newRole}.";
            return RedirectToAction(nameof(Users));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleUserStatus(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            // Prevent self-deactivation of system administrator
            if (user.Id == User.FindFirstValue(ClaimTypes.NameIdentifier))
            {
                TempData["ErrorMessage"] = "You cannot deactivate your own administrative account.";
                return RedirectToAction(nameof(Users));
            }

            user.IsActive = !user.IsActive;
            await _userManager.UpdateAsync(user);

            await _auditService.LogActionAsync(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                User.FindFirstValue(ClaimTypes.Email),
                "UserStatusToggled",
                "ApplicationUser",
                user.Id,
                $"User account {user.Email} status set to {(user.IsActive ? "Active" : "Inactive")}.");

            TempData["SuccessMessage"] = $"User {user.FullName} is now {(user.IsActive ? "Active" : "Inactive")}.";
            return RedirectToAction(nameof(Users));
        }
    }

    public class UserManagementViewModel
    {
        public string UserId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? RegistrationNumber { get; set; }
        public string? PhoneNumber { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CurrentRole { get; set; } = string.Empty;
    }
}
