using FluentValidation;
using Yakku.Application.Common.Exceptions;
using Yakku.Domain.Enums;

namespace Yakku.Application.Audit
{
    /// <summary>
    /// Builds sanitized <see cref="SystemLogEntry"/> rows for exceptions. Messages and metadata are scrubbed
    /// so tokens, emails, phones and secrets never land in <c>system_logs</c>.
    /// </summary>
    public static class ExceptionSystemLog
    {
        public static SystemLogEntry Create(
            Exception exception,
            string source,
            string? httpMethod = null,
            string? path = null,
            int? statusCode = null,
            SystemLogCategory category = SystemLogCategory.Exception,
            IReadOnlyDictionary<string, string?>? extraMetadata = null)
        {
            var root = Unwrap(exception);
            var status = statusCode ?? InferStatusCode(root);
            var severity = status >= 500
                ? SystemLogSeverity.Error
                : status >= 400
                    ? SystemLogSeverity.Warning
                    : SystemLogSeverity.Error;

            var metadata = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            if (extraMetadata is not null)
            {
                foreach (var pair in extraMetadata)
                {
                    metadata[pair.Key] = pair.Value;
                }
            }

            metadata["exception"] = root.GetType().FullName ?? root.GetType().Name;
            if (root.InnerException is { } inner)
            {
                metadata["innerException"] = inner.GetType().FullName ?? inner.GetType().Name;
                var innerMessage = AuditSanitizer.SanitizeText(inner.Message, 200);
                if (innerMessage is not null)
                {
                    metadata["innerMessage"] = innerMessage;
                }
            }

            string message;
            if (root is AppException app)
            {
                status = app.StatusCode;
                severity = app.StatusCode >= 500 ? SystemLogSeverity.Error : SystemLogSeverity.Warning;
                message = AuditSanitizer.SanitizeText(app.ErrorMessage, 500)
                    ?? AuditSanitizer.SanitizeText(app.Message, 500)
                    ?? "Application error.";
                metadata["errorCode"] = AuditSanitizer.SanitizeCode(app.ErrorCode, 64);
                if (!string.IsNullOrWhiteSpace(app.Field))
                {
                    metadata["field"] = AuditSanitizer.SanitizeCode(app.Field, 64);
                }
            }
            else if (root is ValidationException validation)
            {
                status = StatusCodes.Status400BadRequest;
                severity = SystemLogSeverity.Warning;
                message = "Validation failed.";
                var errors = validation.Errors as IList<FluentValidation.Results.ValidationFailure>
                    ?? validation.Errors.ToList();
                metadata["errorCount"] = errors.Count.ToString();
                var first = errors.FirstOrDefault();
                if (first is not null)
                {
                    metadata["firstField"] = AuditSanitizer.SanitizeCode(first.PropertyName, 64);
                    metadata["firstError"] = AuditSanitizer.SanitizeText(first.ErrorMessage, 200);
                }
            }
            else
            {
                message = AuditSanitizer.SanitizeText(root.Message, 500)
                    ?? "Unhandled exception.";
            }

            return new SystemLogEntry(
                category,
                severity,
                source,
                message,
                ExceptionType: root.GetType().FullName ?? root.GetType().Name,
                HttpMethod: httpMethod,
                Path: path,
                StatusCode: status,
                Metadata: metadata);
        }

        public static Task WriteAsync(
            IAuditLogWriter audit,
            Exception exception,
            string source,
            string? httpMethod = null,
            string? path = null,
            int? statusCode = null,
            SystemLogCategory category = SystemLogCategory.Exception,
            IReadOnlyDictionary<string, string?>? extraMetadata = null,
            CancellationToken cancellationToken = default)
        {
            return audit.WriteSystemAsync(
                Create(exception, source, httpMethod, path, statusCode, category, extraMetadata),
                cancellationToken);
        }

        private static Exception Unwrap(Exception exception)
        {
            if (exception is AggregateException aggregate)
            {
                return aggregate.Flatten().InnerExceptions.FirstOrDefault() ?? aggregate;
            }

            return exception;
        }

        private static int InferStatusCode(Exception exception) => exception switch
        {
            AppException app => app.StatusCode,
            ValidationException => 400,
            OperationCanceledException => 499,
            _ => 500
        };

        /// <summary>Avoids a hard dependency on ASP.NET StatusCodes from Application.</summary>
        private static class StatusCodes
        {
            public const int Status400BadRequest = 400;
        }
    }
}
