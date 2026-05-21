using Microsoft.AspNetCore.Authorization;
using SecureGate.Domain.Auth;
using SecureGate.Infrastructure.Services.Interfaces;

namespace SecureGate.Api.Filters
{
    public class PermissionRequirement : IAuthorizationRequirement
    {
        public Permission Permission { get; }

        public PermissionRequirement(Permission permission)
        {
            Permission = permission;
        }
    }

    public class PermissionHandler : AuthorizationHandler<PermissionRequirement>
    {
        private readonly IPermissionService _permissionService;

        public PermissionHandler(IPermissionService permissionService)
        {
            _permissionService = permissionService;
        }

        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            PermissionRequirement requirement)
        {
            if (await _permissionService.HasPermissionAsync(context.User, requirement.Permission))
            {
                context.Succeed(requirement);
            }
        }
    }
}
