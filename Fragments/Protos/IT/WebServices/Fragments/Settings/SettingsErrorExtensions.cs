using System;
using System.Collections.Generic;
using System.Linq;
using IT.WebServices.Fragments.Settings;
using ProtoValidate;

namespace IT.WebServices.Fragments.Settings
{
    public static class SettingsErrorExtensions
    {
        public static SettingsError CreateError(SettingsErrorReason errorType, string message)
        {
            return new SettingsError
            {
                Type = errorType,
                Message = message ?? string.Empty
            };
        }

        public static SettingsError AddValidationIssue(this SettingsError error, string field, string message, string code = "")
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

        public static SettingsError FromProtoValidateResult(ValidationResult validationResult, SettingsErrorReason errorType, string message = "Validation failed")
        {
            if (validationResult == null)
                throw new ArgumentNullException(nameof(validationResult));

            var error = new SettingsError
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
        public static SettingsError CreateUnauthorizedError(string operation = "")
        {
            var message = string.IsNullOrEmpty(operation) 
                ? "Unauthorized settings operation" 
                : $"Unauthorized to {operation}";
            return CreateError(SettingsErrorReason.SettingsErrorUnauthorized, message);
        }

        public static SettingsError CreateValidationError(string message = "Validation failed")
        {
            return CreateError(SettingsErrorReason.SettingsErrorValidationFailed, message);
        }

        public static SettingsError CreateServiceOfflineError()
        {
            return CreateError(SettingsErrorReason.SettingsErrorServiceOffline, "Settings service is currently unavailable");
        }

        public static SettingsError CreateNotFoundError(string settingName = "")
        {
            var message = string.IsNullOrEmpty(settingName) 
                ? "Setting not found" 
                : $"Setting '{settingName}' not found";
            return CreateError(SettingsErrorReason.SettingsErrorUnauthorized, message); // Using unauthorized as there's no specific not found reason
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