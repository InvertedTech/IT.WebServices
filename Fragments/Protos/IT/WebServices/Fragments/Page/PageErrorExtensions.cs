using System;
using System.Collections.Generic;
using System.Linq;
using IT.WebServices.Fragments.Page;
using ProtoValidate;

namespace IT.WebServices.Fragments.Page
{
    public static class PageErrorExtensions
    {
        public static PageError CreateError(PageErrorReason errorType, string message)
        {
            return new PageError
            {
                Type = errorType,
                Message = message ?? string.Empty
            };
        }

        public static PageError AddValidationIssue(this PageError error, string field, string message, string code = "")
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

        public static PageError FromProtoValidateResult(ValidationResult validationResult, PageErrorReason errorType, string message = "Validation failed")
        {
            if (validationResult == null)
                throw new ArgumentNullException(nameof(validationResult));

            var error = new PageError
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

        // Service-specific helper methods for CMS scenarios
        public static PageError CreatePageNotFoundError(string pageId = "")
        {
            var message = string.IsNullOrEmpty(pageId) 
                ? "Page not found" 
                : $"Page '{pageId}' not found";
            return CreateError(PageErrorReason.GetPageErrorNotFound, message);
        }

        public static PageError CreateUnauthorizedPageError(string operation = "")
        {
            var message = string.IsNullOrEmpty(operation) 
                ? "Unauthorized page operation" 
                : $"Unauthorized to {operation} page";
            return CreateError(PageErrorReason.GetPageErrorUnauthorized, message);
        }

        public static PageError CreateUrlConflictError(string url = "")
        {
            var message = string.IsNullOrEmpty(url) 
                ? "Page URL already exists" 
                : $"Page URL '{url}' already exists";
            return CreateError(PageErrorReason.CreatePageErrorUrlConflict, message);
        }

        public static PageError CreateInvalidContentError(string details = "")
        {
            var message = string.IsNullOrEmpty(details) 
                ? "Invalid page content" 
                : $"Invalid page content: {details}";
            return CreateError(PageErrorReason.CreatePageErrorInvalidContent, message);
        }

        public static PageError CreateInvalidPublishDateError(string details = "")
        {
            var message = string.IsNullOrEmpty(details) 
                ? "Invalid publish date" 
                : $"Invalid publish date: {details}";
            return CreateError(PageErrorReason.PublishPageErrorInvalidDate, message);
        }

        public static PageError CreateInvalidSearchQueryError(string query = "")
        {
            var message = string.IsNullOrEmpty(query) 
                ? "Invalid search query" 
                : $"Invalid search query: {query}";
            return CreateError(PageErrorReason.SearchPageErrorInvalidQuery, message);
        }

        public static PageError CreateServiceOfflineError()
        {
            return CreateError(PageErrorReason.PageErrorServiceOffline, "Page service is currently unavailable");
        }

        public static PageError CreateValidationError(string message = "Validation failed")
        {
            return CreateError(PageErrorReason.PageErrorValidationFailed, message);
        }

        public static PageError CreateUnauthorizedError(string operation = "")
        {
            var message = string.IsNullOrEmpty(operation) 
                ? "Unauthorized page operation" 
                : $"Unauthorized to {operation}";
            return CreateError(PageErrorReason.GetPageErrorUnauthorized, message);
        }

        public static PageError CreateNotFoundError(string pageId = "")
        {
            var message = string.IsNullOrEmpty(pageId) 
                ? "Page not found" 
                : $"Page '{pageId}' not found";
            return CreateError(PageErrorReason.GetPageErrorNotFound, message);
        }

        // CMS-specific helper methods
        public static PageError CreateCreatePageNotFoundError(string pageId = "")
        {
            var message = string.IsNullOrEmpty(pageId) 
                ? "Page not found for creation operation" 
                : $"Page '{pageId}' not found for creation operation";
            return CreateError(PageErrorReason.CreatePageErrorUnknown, message);
        }

        public static PageError CreateModifyPageNotFoundError(string pageId = "")
        {
            var message = string.IsNullOrEmpty(pageId) 
                ? "Page not found for modification" 
                : $"Page '{pageId}' not found for modification";
            return CreateError(PageErrorReason.ModifyPageErrorNotFound, message);
        }

        public static PageError CreateModifyPageUnauthorizedError(string pageId = "")
        {
            var message = string.IsNullOrEmpty(pageId) 
                ? "Unauthorized to modify page" 
                : $"Unauthorized to modify page '{pageId}'";
            return CreateError(PageErrorReason.ModifyPageErrorUnauthorized, message);
        }

        public static PageError CreateModifyPageUrlConflictError(string url = "")
        {
            var message = string.IsNullOrEmpty(url) 
                ? "URL conflict during page modification" 
                : $"URL '{url}' conflicts with existing page during modification";
            return CreateError(PageErrorReason.ModifyPageErrorUrlConflict, message);
        }

        public static PageError CreatePublishPageNotFoundError(string pageId = "")
        {
            var message = string.IsNullOrEmpty(pageId) 
                ? "Page not found for publishing" 
                : $"Page '{pageId}' not found for publishing";
            return CreateError(PageErrorReason.PublishPageErrorNotFound, message);
        }

        public static PageError CreatePublishPageUnauthorizedError(string pageId = "")
        {
            var message = string.IsNullOrEmpty(pageId) 
                ? "Unauthorized to publish page" 
                : $"Unauthorized to publish page '{pageId}'";
            return CreateError(PageErrorReason.PublishPageErrorUnauthorized, message);
        }

        public static PageError CreateDeletePageNotFoundError(string pageId = "")
        {
            var message = string.IsNullOrEmpty(pageId) 
                ? "Page not found for deletion" 
                : $"Page '{pageId}' not found for deletion";
            return CreateError(PageErrorReason.DeletePageErrorNotFound, message);
        }

        public static PageError CreateDeletePageUnauthorizedError(string pageId = "")
        {
            var message = string.IsNullOrEmpty(pageId) 
                ? "Unauthorized to delete page" 
                : $"Unauthorized to delete page '{pageId}'";
            return CreateError(PageErrorReason.DeletePageErrorUnauthorized, message);
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