using System;
using System.Collections.Generic;
using System.Linq;
using IT.WebServices.Fragments.Notification;
using ProtoValidate;

namespace IT.WebServices.Fragments.Notification
{
    public static class NotificationErrorExtensions
    {
        public static NotificationError CreateError(NotificationErrorReason errorType, string message)
        {
            return new NotificationError
            {
                Type = errorType,
                Message = message ?? string.Empty
            };
        }

        public static NotificationError AddValidationIssue(this NotificationError error, string field, string message, string code = "")
        {
            if (error == null)
                throw new ArgumentNullException(nameof(error));

            error.Validation.Add(new IT.WebServices.Fragments.ValidationIssue
            {
                Field = field ?? string.Empty,
                Message = message ?? string.Empty,
                Code = code ?? string.Empty
            });

            return error;
        }

        public static NotificationError FromProtoValidateResult(ValidationResult validationResult, NotificationErrorReason errorType, string message = "Validation failed")
        {
            if (validationResult == null)
                throw new ArgumentNullException(nameof(validationResult));

            var error = new NotificationError
            {
                Type = errorType,
                Message = message ?? "Validation failed"
            };

            if (validationResult.Violations?.Count > 0)
            {
                foreach (var violation in validationResult.Violations)
                {
                    error.AddValidationIssue(
                        GetFieldPath(violation),
                        GetStringProperty(violation, "Message"),
                        GetRuleId(violation)
                    );
                }
            }

            return error;
        }

        // Service-specific helper methods
        public static NotificationError CreateInvalidAddressError(string address = "")
        {
            var message = string.IsNullOrEmpty(address) 
                ? "Invalid email address" 
                : $"Invalid email address: {address}";
            return CreateError(NotificationErrorReason.SendEmailErrorInvalidAddress, message);
        }

        public static NotificationError CreateDeliveryFailedError(string details = "")
        {
            var message = string.IsNullOrEmpty(details) 
                ? "Email delivery failed" 
                : $"Email delivery failed: {details}";
            return CreateError(NotificationErrorReason.SendEmailErrorDeliveryFailed, message);
        }

        public static NotificationError CreateTemplateError(string details = "")
        {
            var message = string.IsNullOrEmpty(details) 
                ? "Email template processing error" 
                : $"Email template error: {details}";
            return CreateError(NotificationErrorReason.SendEmailErrorTemplateError, message);
        }

        public static NotificationError CreateRateLimitedError(string details = "")
        {
            var message = string.IsNullOrEmpty(details) 
                ? "Email sending rate limit exceeded" 
                : $"Rate limit exceeded: {details}";
            return CreateError(NotificationErrorReason.SendEmailErrorRateLimited, message);
        }

        public static NotificationError CreateServiceOfflineError()
        {
            return CreateError(NotificationErrorReason.NotificationErrorServiceOffline, "Notification service is currently unavailable");
        }

        public static NotificationError CreateValidationError(string message = "Validation failed")
        {
            return CreateError(NotificationErrorReason.NotificationErrorValidationFailed, message);
        }

        public static NotificationError CreateUnauthorizedError(string operation = "")
        {
            var message = string.IsNullOrEmpty(operation) 
                ? "Unauthorized notification operation" 
                : $"Unauthorized to {operation}";
            return CreateError(NotificationErrorReason.NotificationErrorUnauthorized, message);
        }

        public static NotificationError CreateNotFoundError(string notificationId = "")
        {
            var message = string.IsNullOrEmpty(notificationId) 
                ? "Notification not found" 
                : $"Notification '{notificationId}' not found";
            return CreateError(NotificationErrorReason.NotificationErrorUnauthorized, message); // Using unauthorized as there's no specific not found reason
        }

        private static string GetStringProperty(object obj, params string[] propertyNames)
        {
            if (obj == null || propertyNames == null)
                return string.Empty;

            foreach (var propertyName in propertyNames)
            {
                var property = obj.GetType().GetProperty(propertyName);
                if (property == null)
                    continue;

                var value = property.GetValue(obj);
                if (value == null)
                    continue;

                var stringValue = value.ToString();
                if (!string.IsNullOrWhiteSpace(stringValue))
                    return stringValue;
            }

            return string.Empty;
        }

        private static string GetFieldPath(object violation)
        {
            if (violation == null)
                return string.Empty;

            var simple = GetStringProperty(violation, "Field", "Path");
            if (!string.IsNullOrWhiteSpace(simple))
                return simple;
            var fieldPathProperty = violation.GetType().GetProperty("FieldPath");
            var fieldPath = fieldPathProperty?.GetValue(violation);
            if (fieldPath != null)
            {
                var fieldPathString = fieldPath.ToString();
                if (!string.IsNullOrWhiteSpace(fieldPathString))
                    return fieldPathString;


                var segmentsProperty = fieldPath.GetType().GetProperty("Segments");
                var segments = segmentsProperty?.GetValue(fieldPath) as System.Collections.IEnumerable;
                if (segments != null)
                {
                    var parts = new List<string>();
                    foreach (var segment in segments)
                    {
                        var name = GetStringProperty(segment, "Field", "Name");
                        if (!string.IsNullOrWhiteSpace(name))
                            parts.Add(name);
                    }
                    if (parts.Count > 0)
                        return string.Join(".", parts);
                }
            }

            return string.Empty;
        }

        private static string GetRuleId(object violation)
        {
            if (violation == null)
                return string.Empty;

            var id = GetStringProperty(violation, "ConstraintId", "RuleId");
            if (!string.IsNullOrWhiteSpace(id))
                return id;
            var ruleProperty = violation.GetType().GetProperty("Rule");
            var rule = ruleProperty?.GetValue(violation);
            if (rule != null)
            {
                id = GetStringProperty(rule, "Id", "Name");
                if (!string.IsNullOrWhiteSpace(id))
                    return id;
            }

            return string.Empty;
        }
    }
}