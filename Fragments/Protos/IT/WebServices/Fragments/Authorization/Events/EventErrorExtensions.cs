using System;
using System.Collections.Generic;
using System.Linq;
using IT.WebServices.Fragments.Authorization.Events;
using ProtoValidate;

namespace IT.WebServices.Fragments.Authorization.Events
{
    public static class EventErrorExtensions
    {
        public static EventError CreateError(EventErrorReason errorType, string message)
        {
            return new EventError
            {
                Type = errorType,
                Message = message ?? string.Empty
            };
        }

        public static EventError AddValidationIssue(this EventError error, string field, string message, string code = "")
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

        public static EventError FromProtoValidateResult(ValidationResult validationResult, EventErrorReason errorType, string message = "Validation failed")
        {
            if (validationResult == null)
                throw new ArgumentNullException(nameof(validationResult));

            var error = new EventError
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

        // Event-specific helper methods
        public static EventError CreateEventNotFoundError(string eventId = "")
        {
            var message = string.IsNullOrEmpty(eventId) 
                ? "Event not found" 
                : $"Event '{eventId}' not found";
            return CreateError(EventErrorReason.GetEventErrorNotFound, message);
        }

        public static EventError CreateUnauthorizedEventError(string operation = "")
        {
            var message = string.IsNullOrEmpty(operation) 
                ? "Unauthorized event operation" 
                : $"Unauthorized to {operation} event";
            return CreateError(EventErrorReason.EventErrorUnauthorized, message);
        }

        public static EventError CreateInvalidRequestError(string details = "")
        {
            var message = string.IsNullOrEmpty(details) 
                ? "Invalid request" 
                : $"Invalid request: {details}";
            return CreateError(EventErrorReason.CreateEventErrorInvalidRequest, message);
        }

        public static EventError CreateEventAlreadyExistsError(string eventId = "")
        {
            var message = string.IsNullOrEmpty(eventId) 
                ? "Event already exists" 
                : $"Event '{eventId}' already exists";
            return CreateError(EventErrorReason.CreateEventErrorAlreadyExists, message);
        }

        public static EventError CreateInvalidRecurrenceError(string details = "")
        {
            var message = string.IsNullOrEmpty(details) 
                ? "Invalid recurrence rule" 
                : $"Invalid recurrence rule: {details}";
            return CreateError(EventErrorReason.CreateRecurringEventErrorInvalidRecurrence, message);
        }

        public static EventError CreateInvalidHashError(string hash = "")
        {
            var message = string.IsNullOrEmpty(hash) 
                ? "Invalid recurrence hash" 
                : $"Invalid recurrence hash: {hash}";
            return CreateError(EventErrorReason.CreateRecurringEventErrorInvalidHash, message);
        }

        // Ticket-specific helper methods
        public static EventError CreateTicketNotFoundError(string ticketId = "")
        {
            var message = string.IsNullOrEmpty(ticketId) 
                ? "Ticket not found" 
                : $"Ticket '{ticketId}' not found";
            return CreateError(EventErrorReason.CancelTicketErrorTicketNotFound, message);
        }

        public static EventError CreateUnauthorizedTicketError(string operation = "")
        {
            var message = string.IsNullOrEmpty(operation) 
                ? "Unauthorized ticket operation" 
                : $"Unauthorized to {operation} ticket";
            return CreateError(EventErrorReason.ReserveTicketErrorUnauthorized, message);
        }

        public static EventError CreateMaxLimitReachedError(string eventId = "")
        {
            var message = string.IsNullOrEmpty(eventId) 
                ? "Maximum ticket limit reached" 
                : $"Maximum ticket limit reached for event '{eventId}'";
            return CreateError(EventErrorReason.ReserveTicketErrorMaxLimitReached, message);
        }

        public static EventError CreateTicketsNotOnSaleError(string eventId = "")
        {
            var message = string.IsNullOrEmpty(eventId) 
                ? "Tickets are not currently on sale" 
                : $"Tickets for event '{eventId}' are not currently on sale";
            return CreateError(EventErrorReason.ReserveTicketErrorNotOnSale, message);
        }

        public static EventError CreateTicketAlreadyUsedError(string ticketId = "")
        {
            var message = string.IsNullOrEmpty(ticketId) 
                ? "Ticket has already been used" 
                : $"Ticket '{ticketId}' has already been used";
            return CreateError(EventErrorReason.UseTicketErrorAlreadyUsed, message);
        }

        public static EventError CreateTicketExpiredError(string ticketId = "")
        {
            var message = string.IsNullOrEmpty(ticketId) 
                ? "Ticket has expired" 
                : $"Ticket '{ticketId}' has expired";
            return CreateError(EventErrorReason.UseTicketErrorExpired, message);
        }

        public static EventError CreateTicketCanceledError(string ticketId = "")
        {
            var message = string.IsNullOrEmpty(ticketId) 
                ? "Ticket has been canceled" 
                : $"Ticket '{ticketId}' has been canceled";
            return CreateError(EventErrorReason.UseTicketErrorCanceled, message);
        }

        // Generic helper methods
        public static EventError CreateServiceOfflineError()
        {
            return CreateError(EventErrorReason.EventErrorServiceOffline, "Event service is currently unavailable");
        }

        public static EventError CreateValidationError(string message = "Validation failed")
        {
            return CreateError(EventErrorReason.EventErrorValidationFailed, message);
        }

        public static EventError CreateUnauthorizedError(string operation = "")
        {
            var message = string.IsNullOrEmpty(operation) 
                ? "Unauthorized event operation" 
                : $"Unauthorized to {operation}";
            return CreateError(EventErrorReason.EventErrorUnauthorized, message);
        }

        public static EventError CreateNotFoundError(string eventId = "")
        {
            var message = string.IsNullOrEmpty(eventId) 
                ? "Event not found" 
                : $"Event '{eventId}' not found";
            return CreateError(EventErrorReason.GetEventErrorNotFound, message);
        }

        public static EventError CreateNullBodyError()
        {
            return CreateError(EventErrorReason.CreateEventErrorNullBody, "Request body cannot be null");
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