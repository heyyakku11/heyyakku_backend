using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Yakku.API.Swagger
{
    public sealed class AuthorizeCheckOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            if (!RequiresAuthorization(context))
            {
                return;
            }

            operation.Security =
            [
                new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference("Bearer", context.Document)] = []
                }
            ];
        }

        private static bool RequiresAuthorization(OperationFilterContext context)
        {
            var metadata = context.ApiDescription.ActionDescriptor.EndpointMetadata;

            // Guest votes use cookie identity; still show the lock in Swagger.
            if (string.Equals(context.MethodInfo.Name, "GuestVote", StringComparison.Ordinal)
                || context.ApiDescription.RelativePath?.Contains("guest-votes", StringComparison.OrdinalIgnoreCase) == true)
            {
                return true;
            }

            if (metadata.OfType<IAllowAnonymous>().Any()
                || HasAttribute<AllowAnonymousAttribute>(context.MethodInfo))
            {
                return false;
            }

            return metadata.OfType<IAuthorizeData>().Any()
                || HasAttribute<AuthorizeAttribute>(context.MethodInfo);
        }

        private static bool HasAttribute<T>(MethodInfo methodInfo)
            where T : Attribute
        {
            return methodInfo.GetCustomAttributes(true).OfType<T>().Any()
                || methodInfo.DeclaringType?.GetCustomAttributes(true).OfType<T>().Any() == true;
        }
    }
}
