using System;
using System.Collections.Generic;
using ProtoValidate;

namespace IT.WebServices.Fragments
{
    public static class GenericErrorExtensions
    {
        public static APIError Create(APIErrorReason reason, string message)
        {
            return new APIError
            {
                Reason = reason,
                Message = message ?? string.Empty
            };
        }

        public static APIError CreateError(APIErrorReason reason, string message) => Create(reason, message);

        public static APIError CreateNoError(string message = "No Error") => Create(APIErrorReason.ErrorReasonNoError, message);

        public static APIError AddValidationIssue(this APIError error, string field, string message, string code = "")
        {
            if (error == null)
                throw new ArgumentNullException(nameof(error));

            error.Validation.Add(
                new ValidationIssue
                {
                    Field = field ?? string.Empty,
                    Message = message ?? string.Empty,
                    Code = code ?? string.Empty
                }
            );

            return error;
        }

        public static APIError FromProtoValidateResult(ValidationResult validationResult, APIErrorReason errorType, string message = "Validation failed")
        {
            if (validationResult == null)
                throw new ArgumentNullException(nameof(validationResult));

            var error = new APIError
            {
                Reason = errorType,
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
