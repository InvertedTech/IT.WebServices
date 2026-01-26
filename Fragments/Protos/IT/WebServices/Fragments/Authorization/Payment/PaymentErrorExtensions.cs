using System;
using System.Collections.Generic;
using System.Linq;
using IT.WebServices.Fragments.Authorization.Payment;
using ProtoValidate;

namespace IT.WebServices.Fragments.Authorization.Payment
{
    public static class PaymentErrorExtensions
    {
        public static PaymentError CreateError(PaymentErrorReason errorType, string message)
        {
            return new PaymentError
            {
                Type = errorType,
                Message = message ?? string.Empty
            };
        }

        public static PaymentError AddValidationIssue(this PaymentError error, string field, string message, string code = "")
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

        public static PaymentError FromProtoValidateResult(ValidationResult validationResult, PaymentErrorReason errorType, string message = "Validation failed")
        {
            if (validationResult == null)
                throw new ArgumentNullException(nameof(validationResult));

            var error = new PaymentError
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

        // Service-specific helper methods for payment scenarios
        public static PaymentError CreateSubscriptionNotFoundError(string subscriptionId = "")
        {
            var message = string.IsNullOrEmpty(subscriptionId) 
                ? "Subscription not found" 
                : $"Subscription '{subscriptionId}' not found";
            return CreateError(PaymentErrorReason.GetSubscriptionErrorNotFound, message);
        }

        public static PaymentError CreatePaymentNotFoundError(string paymentId = "")
        {
            var message = string.IsNullOrEmpty(paymentId) 
                ? "Payment not found" 
                : $"Payment '{paymentId}' not found";
            return CreateError(PaymentErrorReason.GetPaymentErrorNotFound, message);
        }

        public static PaymentError CreateUnauthorizedError(string operation = "")
        {
            var message = string.IsNullOrEmpty(operation) 
                ? "Unauthorized payment operation" 
                : $"Unauthorized to {operation}";
            return CreateError(PaymentErrorReason.PaymentErrorUnauthorized, message);
        }

        public static PaymentError CreateNotFoundError(string paymentId = "")
        {
            var message = string.IsNullOrEmpty(paymentId) 
                ? "Payment not found" 
                : $"Payment '{paymentId}' not found";
            return CreateError(PaymentErrorReason.GetPaymentErrorNotFound, message);
        }

        public static PaymentError CreateValidationError(string message = "Validation failed")
        {
            return CreateError(PaymentErrorReason.PaymentErrorValidationFailed, message);
        }

        public static PaymentError CreateServiceOfflineError()
        {
            return CreateError(PaymentErrorReason.PaymentErrorServiceOffline, "Payment service is currently unavailable");
        }

        public static PaymentError CreateProviderError(string provider = "", string details = "")
        {
            var message = string.IsNullOrEmpty(provider) 
                ? "Payment provider error" 
                : $"Payment provider '{provider}' error";
            
            if (!string.IsNullOrEmpty(details))
                message += $": {details}";
                
            return CreateError(PaymentErrorReason.GetNewDetailsErrorProviderError, message);
        }

        public static PaymentError CreateSubscriptionAlreadyCanceledError(string subscriptionId = "")
        {
            var message = string.IsNullOrEmpty(subscriptionId) 
                ? "Subscription is already canceled" 
                : $"Subscription '{subscriptionId}' is already canceled";
            return CreateError(PaymentErrorReason.CancelSubscriptionErrorAlreadyCanceled, message);
        }

        public static PaymentError CreateInvalidLevelError(string level = "")
        {
            var message = string.IsNullOrEmpty(level) 
                ? "Invalid subscription level" 
                : $"Invalid subscription level: {level}";
            return CreateError(PaymentErrorReason.GetNewDetailsErrorInvalidLevel, message);
        }

        public static PaymentError CreateBulkActionError(string action = "", string details = "")
        {
            var message = string.IsNullOrEmpty(action) 
                ? "Bulk action failed" 
                : $"Bulk action '{action}' failed";
            
            if (!string.IsNullOrEmpty(details))
                message += $": {details}";
                
            return CreateError(PaymentErrorReason.AdminBulkActionErrorInvalidAction, message);
        }

        // Private helper methods (copied from ErrorExtensions.cs pattern)
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