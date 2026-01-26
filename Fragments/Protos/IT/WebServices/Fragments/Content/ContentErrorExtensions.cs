using System;
using System.Collections.Generic;
using System.Linq;
using IT.WebServices.Fragments.Content;
using ProtoValidate;

namespace IT.WebServices.Fragments.Content
{
    public static class ContentErrorExtensions
    {
        public static ContentError CreateError(ContentErrorReason errorType, string message)
        {
            return new ContentError
            {
                Type = errorType,
                Message = message ?? string.Empty
            };
        }

        public static ContentError AddValidationIssue(this ContentError error, string field, string message, string code = "")
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

        public static ContentError FromProtoValidateResult(ValidationResult validationResult, ContentErrorReason errorType, string message = "Validation failed")
        {
            if (validationResult == null)
                throw new ArgumentNullException(nameof(validationResult));

            var error = new ContentError
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
        public static ContentError CreateAssetNotFoundError(string assetId = "")
        {
            var message = string.IsNullOrEmpty(assetId) 
                ? "Asset not found" 
                : $"Asset '{assetId}' not found";
            return CreateError(ContentErrorReason.GetAssetErrorNotFound, message);
        }

        public static ContentError CreateUnauthorizedAssetError(string operation = "")
        {
            var message = string.IsNullOrEmpty(operation) 
                ? "Unauthorized asset operation" 
                : $"Unauthorized to {operation} asset";
            return CreateError(ContentErrorReason.GetAssetErrorUnauthorized, message);
        }

        public static ContentError CreateInvalidFormatError(string format = "")
        {
            var message = string.IsNullOrEmpty(format) 
                ? "Invalid asset format" 
                : $"Invalid asset format: {format}";
            return CreateError(ContentErrorReason.CreateAssetErrorInvalidFormat, message);
        }

        public static ContentError CreateFileTooLargeError(string details = "")
        {
            var message = string.IsNullOrEmpty(details) 
                ? "File size exceeds maximum allowed" 
                : $"File size exceeds maximum allowed: {details}";
            return CreateError(ContentErrorReason.CreateAssetErrorFileTooLarge, message);
        }

        public static ContentError CreateUploadFailedError(string details = "")
        {
            var message = string.IsNullOrEmpty(details) 
                ? "Asset upload failed" 
                : $"Asset upload failed: {details}";
            return CreateError(ContentErrorReason.CreateAssetErrorUploadFailed, message);
        }

        public static ContentError CreateInvalidSearchQueryError(string query = "")
        {
            var message = string.IsNullOrEmpty(query) 
                ? "Invalid search query" 
                : $"Invalid search query: {query}";
            return CreateError(ContentErrorReason.SearchAssetErrorInvalidQuery, message);
        }

        public static ContentError CreateServiceOfflineError()
        {
            return CreateError(ContentErrorReason.ContentErrorServiceOffline, "Content service is currently unavailable");
        }

        public static ContentError CreateValidationError(string message = "Validation failed")
        {
            return CreateError(ContentErrorReason.ContentErrorValidationFailed, message);
        }

        public static ContentError CreateUnauthorizedError(string operation = "")
        {
            var message = string.IsNullOrEmpty(operation) 
                ? "Unauthorized content operation" 
                : $"Unauthorized to {operation}";
            return CreateError(ContentErrorReason.ContentErrorUnauthorized, message);
        }

        public static ContentError CreateNotFoundError(string assetId = "")
        {
            var message = string.IsNullOrEmpty(assetId) 
                ? "Asset not found" 
                : $"Asset '{assetId}' not found";
            return CreateError(ContentErrorReason.GetAssetErrorNotFound, message);
        }

        // Admin-specific helper methods
        public static ContentError CreateAdminAssetNotFoundError(string assetId = "")
        {
            var message = string.IsNullOrEmpty(assetId) 
                ? "Asset not found for admin operation" 
                : $"Asset '{assetId}' not found for admin operation";
            return CreateError(ContentErrorReason.AdminAssetErrorNotFound, message);
        }

        public static ContentError CreateAdminUnauthorizedError(string operation = "")
        {
            var message = string.IsNullOrEmpty(operation) 
                ? "Unauthorized admin operation" 
                : $"Unauthorized admin operation: {operation}";
            return CreateError(ContentErrorReason.AdminAssetErrorUnauthorized, message);
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