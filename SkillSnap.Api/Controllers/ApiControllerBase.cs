using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SkillSnap.Api.Models;
using SkillSnap.Shared.Models;

namespace SkillSnap.Api.Controllers
{
    // Base controller exposing the current user's id and role helpers.
    public abstract class ApiControllerBase : ControllerBase
    {
        protected string? CurrentUserId =>
            User.FindFirstValue(ClaimTypes.NameIdentifier);

        protected bool IsAdmin => User.IsInRole(Roles.Admin);
        protected bool IsManager => User.IsInRole(Roles.Manager);

        // Managers and Admins are allowed to read across all users.
        protected bool CanReadAll => IsAdmin || IsManager;
    }
}
