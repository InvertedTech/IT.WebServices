using System;
using System.Collections.Generic;
using System.Linq;
using IT.WebServices.Fragments.Comment;
using ProtoValidate;

namespace IT.WebServices.Fragments.Comment
{
    public static class CommentErrorExtensions
    {
        public static CommentError CreateError(CommentErrorReason errorType, string message)
        {
            return new CommentError
            {
                Type = errorType,
                Message = message ?? string.Empty
            };
        }

        public static CommentError AddValidationIssue(this CommentError error, string field, string message, string code = "")
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

        public static CommentError FromProtoValidateResult(ValidationResult validationResult, CommentErrorReason errorType, string message = "Validation failed")
        {
            if (validationResult == null)
                throw new ArgumentNullException(nameof(validationResult));

            var error = new CommentError
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
        public static CommentError CreateCommentNotFoundError(string commentId = "")
        {
            var message = string.IsNullOrEmpty(commentId) 
                ? "Comment not found" 
                : $"Comment '{commentId}' not found";
            return CreateError(CommentErrorReason.EditCommentErrorNotFound, message);
        }

        public static CommentError CreateUnauthorizedCommentError(string operation = "")
        {
            var message = string.IsNullOrEmpty(operation) 
                ? "Unauthorized comment operation" 
                : $"Unauthorized to {operation} comment";
            return CreateError(CommentErrorReason.EditCommentErrorUnauthorized, message);
        }

        public static CommentError CreateInvalidTextError(string details = "")
        {
            var message = string.IsNullOrEmpty(details) 
                ? "Invalid comment text" 
                : $"Invalid comment text: {details}";
            return CreateError(CommentErrorReason.EditCommentErrorTextInvalid, message);
        }

        public static CommentError CreateContentNotFoundError(string contentId = "")
        {
            var message = string.IsNullOrEmpty(contentId) 
                ? "Content not found for comment" 
                : $"Content '{contentId}' not found for comment";
            return CreateError(CommentErrorReason.CreateCommentErrorContentNotFound, message);
        }

        public static CommentError CreateParentCommentNotFoundError(string parentId = "")
        {
            var message = string.IsNullOrEmpty(parentId) 
                ? "Parent comment not found" 
                : $"Parent comment '{parentId}' not found";
            return CreateError(CommentErrorReason.CreateCommentErrorParentNotFound, message);
        }

        public static CommentError CreateServiceOfflineError()
        {
            return CreateError(CommentErrorReason.CommentErrorServiceOffline, "Comment service is currently unavailable");
        }

        public static CommentError CreateValidationError(string message = "Validation failed")
        {
            return CreateError(CommentErrorReason.CommentErrorValidationFailed, message);
        }

        public static CommentError CreateUnauthorizedError(string operation = "")
        {
            var message = string.IsNullOrEmpty(operation) 
                ? "Unauthorized comment operation" 
                : $"Unauthorized to {operation}";
            return CreateError(CommentErrorReason.EditCommentErrorUnauthorized, message);
        }

        public static CommentError CreateNotFoundError(string commentId = "")
        {
            var message = string.IsNullOrEmpty(commentId) 
                ? "Comment not found" 
                : $"Comment '{commentId}' not found";
            return CreateError(CommentErrorReason.EditCommentErrorNotFound, message);
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