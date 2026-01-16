using System.Reflection;
using System.Text.Json;
using AuthScape.CRM.Interfaces;
using AuthScape.CRM.Models;
using AuthScape.CRM.Models.Enums;
using AuthScape.Models.Users;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace AuthScape.CRM.Services;

/// <summary>
/// Service for dynamically mapping fields between AuthScape entities and CRM records
/// </summary>
public class CrmEntityMapperService : ICrmEntityMapper
{
    private readonly ILogger<CrmEntityMapperService> _logger;

    public CrmEntityMapperService(ILogger<CrmEntityMapperService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Maps an AuthScape entity to CRM fields based on field mappings
    /// </summary>
    public Dictionary<string, object?> MapToCrmFields<T>(T entity, IEnumerable<CrmFieldMapping> fieldMappings) where T : class
    {
        var result = new Dictionary<string, object?>();
        var entityType = typeof(T);

        foreach (var mapping in fieldMappings.Where(m => m.IsEnabled &&
            (m.SyncDirection == CrmSyncDirection.Outbound || m.SyncDirection == CrmSyncDirection.Bidirectional)))
        {
            try
            {
                var value = GetPropertyValue(entity, mapping.AuthScapeField);
                var transformedValue = ApplyTransformation(value, mapping.TransformationType, mapping.TransformationConfig, isOutbound: true);
                result[mapping.CrmField] = transformedValue;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to map field {AuthScapeField} to {CrmField}",
                    mapping.AuthScapeField, mapping.CrmField);
            }
        }

        return result;
    }

    /// <summary>
    /// Maps CRM record fields to an AuthScape entity based on field mappings
    /// </summary>
    public void MapFromCrmFields<T>(T entity, CrmRecord crmRecord, IEnumerable<CrmFieldMapping> fieldMappings) where T : class
    {
        foreach (var mapping in fieldMappings.Where(m => m.IsEnabled &&
            (m.SyncDirection == CrmSyncDirection.Inbound || m.SyncDirection == CrmSyncDirection.Bidirectional)))
        {
            try
            {
                if (crmRecord.Fields.TryGetValue(mapping.CrmField, out var value))
                {
                    var transformedValue = ApplyTransformation(value, mapping.TransformationType, mapping.TransformationConfig, isOutbound: false);
                    SetPropertyValue(entity, mapping.AuthScapeField, transformedValue);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to map CRM field {CrmField} to {AuthScapeField}",
                    mapping.CrmField, mapping.AuthScapeField);
            }
        }
    }

    /// <summary>
    /// Gets a dictionary of changed fields between old and new entity states
    /// </summary>
    public Dictionary<string, object?> GetChangedFields<T>(T? oldEntity, T newEntity, IEnumerable<CrmFieldMapping> fieldMappings) where T : class
    {
        var changes = new Dictionary<string, object?>();

        foreach (var mapping in fieldMappings.Where(m => m.IsEnabled &&
            (m.SyncDirection == CrmSyncDirection.Outbound || m.SyncDirection == CrmSyncDirection.Bidirectional)))
        {
            try
            {
                var newValue = GetPropertyValue(newEntity, mapping.AuthScapeField);
                var oldValue = oldEntity != null ? GetPropertyValue(oldEntity, mapping.AuthScapeField) : null;

                if (!AreValuesEqual(oldValue, newValue))
                {
                    var transformedValue = ApplyTransformation(newValue, mapping.TransformationType, mapping.TransformationConfig, isOutbound: true);
                    changes[mapping.CrmField] = transformedValue;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to compare field {AuthScapeField}", mapping.AuthScapeField);
            }
        }

        return changes;
    }

    /// <summary>
    /// Creates default field mappings for a given AuthScape entity type
    /// </summary>
    public IEnumerable<CrmFieldMapping> GetDefaultFieldMappings(AuthScapeEntityType entityType, CrmProviderType providerType)
    {
        return entityType switch
        {
            AuthScapeEntityType.User => GetDefaultUserMappings(providerType),
            AuthScapeEntityType.Company => GetDefaultCompanyMappings(providerType),
            AuthScapeEntityType.Location => GetDefaultLocationMappings(providerType),
            _ => Enumerable.Empty<CrmFieldMapping>()
        };
    }

    /// <summary>
    /// Gets available AuthScape fields for a given entity type
    /// </summary>
    public IEnumerable<string> GetAvailableAuthScapeFields(AuthScapeEntityType entityType)
    {
        return entityType switch
        {
            AuthScapeEntityType.User => GetUserFields(),
            AuthScapeEntityType.Company => GetCompanyFields(),
            AuthScapeEntityType.Location => GetLocationFields(),
            _ => Enumerable.Empty<string>()
        };
    }

    #region Private Helper Methods

    private object? GetPropertyValue(object entity, string propertyPath)
    {
        var parts = propertyPath.Split('.');
        object? current = entity;

        foreach (var part in parts)
        {
            if (current == null) return null;

            // Handle CustomFields specially
            if (part == "CustomFields" && current is IHasCustomFields customFieldEntity)
            {
                // Next part will be the custom field key
                continue;
            }

            // Check if we're accessing a custom field value
            if (current is IHasCustomFields customFields && parts.Length > 1 && parts[^2] == "CustomFields")
            {
                var customFieldsDict = ParseCustomFields(customFields.CustomFields);
                return customFieldsDict.TryGetValue(part, out var cfValue) ? cfValue : null;
            }

            // Handle dictionary access (for CustomFields stored as Dictionary)
            if (current is IDictionary<string, object?> dict)
            {
                return dict.TryGetValue(part, out var dictValue) ? dictValue : null;
            }

            var type = current.GetType();
            var property = type.GetProperty(part, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            if (property == null)
            {
                _logger.LogWarning("Property {Property} not found on type {Type}", part, type.Name);
                return null;
            }

            current = property.GetValue(current);
        }

        return current;
    }

    private void SetPropertyValue(object entity, string propertyPath, object? value)
    {
        var parts = propertyPath.Split('.');
        object? current = entity;

        for (int i = 0; i < parts.Length - 1; i++)
        {
            if (current == null) return;

            var part = parts[i];
            var type = current.GetType();
            var property = type.GetProperty(part, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            if (property == null) return;
            current = property.GetValue(current);
        }

        if (current == null) return;

        var lastPart = parts[^1];

        // Handle CustomFields specially
        if (parts.Length >= 2 && parts[^2] == "CustomFields" && current is IHasCustomFields customFieldEntity)
        {
            var customFieldsDict = ParseCustomFields(customFieldEntity.CustomFields);
            customFieldsDict[lastPart] = value;
            customFieldEntity.CustomFields = JsonSerializer.Serialize(customFieldsDict);
            return;
        }

        var finalType = current.GetType();
        var finalProperty = finalType.GetProperty(lastPart, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

        if (finalProperty != null && finalProperty.CanWrite)
        {
            var convertedValue = ConvertValue(value, finalProperty.PropertyType);
            finalProperty.SetValue(current, convertedValue);
        }
    }

    private object? ConvertValue(object? value, Type targetType)
    {
        if (value == null)
        {
            return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
        }

        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (underlyingType == typeof(string))
            return value.ToString();

        if (underlyingType == typeof(int) || underlyingType == typeof(int?))
            return Convert.ToInt32(value);

        if (underlyingType == typeof(long) || underlyingType == typeof(long?))
            return Convert.ToInt64(value);

        if (underlyingType == typeof(decimal) || underlyingType == typeof(decimal?))
            return Convert.ToDecimal(value);

        if (underlyingType == typeof(double) || underlyingType == typeof(double?))
            return Convert.ToDouble(value);

        if (underlyingType == typeof(bool) || underlyingType == typeof(bool?))
            return Convert.ToBoolean(value);

        if (underlyingType == typeof(DateTime) || underlyingType == typeof(DateTime?))
            return Convert.ToDateTime(value);

        if (underlyingType == typeof(DateTimeOffset) || underlyingType == typeof(DateTimeOffset?))
        {
            if (value is DateTimeOffset dto) return dto;
            if (value is DateTime dt) return new DateTimeOffset(dt);
            return DateTimeOffset.Parse(value.ToString()!);
        }

        if (underlyingType == typeof(Guid) || underlyingType == typeof(Guid?))
        {
            if (value is Guid g) return g;
            return Guid.Parse(value.ToString()!);
        }

        return value;
    }

    private Dictionary<string, object?> ParseCustomFields(string? customFieldsJson)
    {
        if (string.IsNullOrEmpty(customFieldsJson))
            return new Dictionary<string, object?>();

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(customFieldsJson)
                   ?? new Dictionary<string, object?>();
        }
        catch
        {
            return new Dictionary<string, object?>();
        }
    }

    private bool AreValuesEqual(object? value1, object? value2)
    {
        if (value1 == null && value2 == null) return true;
        if (value1 == null || value2 == null) return false;
        return value1.Equals(value2);
    }

    private object? ApplyTransformation(object? value, string? transformationType, string? config, bool isOutbound)
    {
        if (string.IsNullOrEmpty(transformationType) || transformationType == "None")
            return value;

        return transformationType.ToLowerInvariant() switch
        {
            "uppercase" => value?.ToString()?.ToUpperInvariant(),
            "lowercase" => value?.ToString()?.ToLowerInvariant(),
            "trim" => value?.ToString()?.Trim(),
            "dateformat" => ApplyDateFormatTransformation(value, config, isOutbound),
            "lookup" => ApplyLookupTransformation(value, config, isOutbound),
            "concat" => ApplyConcatTransformation(value, config),
            "split" => ApplySplitTransformation(value, config),
            "default" => value ?? GetDefaultValue(config),
            "boolean" => ApplyBooleanTransformation(value, config, isOutbound),
            _ => value
        };
    }

    private object? ApplyDateFormatTransformation(object? value, string? config, bool isOutbound)
    {
        if (value == null) return null;

        var formatConfig = !string.IsNullOrEmpty(config)
            ? JsonSerializer.Deserialize<DateFormatConfig>(config)
            : null;

        if (isOutbound)
        {
            // Convert AuthScape date to CRM format
            if (value is DateTime dt)
                return dt.ToString(formatConfig?.CrmFormat ?? "yyyy-MM-ddTHH:mm:ssZ");
            if (value is DateTimeOffset dto)
                return dto.ToString(formatConfig?.CrmFormat ?? "yyyy-MM-ddTHH:mm:ssZ");
        }
        else
        {
            // Convert CRM date to AuthScape format
            if (DateTime.TryParse(value.ToString(), out var parsedDate))
                return parsedDate;
        }

        return value;
    }

    private object? ApplyLookupTransformation(object? value, string? config, bool isOutbound)
    {
        if (value == null || string.IsNullOrEmpty(config)) return value;

        try
        {
            var lookupConfig = JsonSerializer.Deserialize<Dictionary<string, string>>(config);
            if (lookupConfig == null) return value;

            var stringValue = value.ToString();
            if (stringValue == null) return value;

            if (isOutbound)
            {
                // AuthScape value -> CRM value
                return lookupConfig.TryGetValue(stringValue, out var crmValue) ? crmValue : value;
            }
            else
            {
                // CRM value -> AuthScape value (reverse lookup)
                var reverseMatch = lookupConfig.FirstOrDefault(kvp => kvp.Value == stringValue);
                return reverseMatch.Key ?? value;
            }
        }
        catch
        {
            return value;
        }
    }

    private object? ApplyConcatTransformation(object? value, string? config)
    {
        if (value == null || string.IsNullOrEmpty(config)) return value;

        try
        {
            var concatConfig = JsonSerializer.Deserialize<ConcatConfig>(config);
            if (concatConfig == null) return value;

            var prefix = concatConfig.Prefix ?? "";
            var suffix = concatConfig.Suffix ?? "";
            return $"{prefix}{value}{suffix}";
        }
        catch
        {
            return value;
        }
    }

    private object? ApplySplitTransformation(object? value, string? config)
    {
        if (value == null || string.IsNullOrEmpty(config)) return value;

        try
        {
            var splitConfig = JsonSerializer.Deserialize<SplitConfig>(config);
            if (splitConfig == null) return value;

            var parts = value.ToString()?.Split(splitConfig.Delimiter ?? " ");
            if (parts == null || parts.Length == 0) return value;

            var index = splitConfig.Index ?? 0;
            return index < parts.Length ? parts[index] : value;
        }
        catch
        {
            return value;
        }
    }

    private object? GetDefaultValue(string? config)
    {
        if (string.IsNullOrEmpty(config)) return null;

        try
        {
            var defaultConfig = JsonSerializer.Deserialize<DefaultValueConfig>(config);
            return defaultConfig?.Value;
        }
        catch
        {
            return config;
        }
    }

    private object? ApplyBooleanTransformation(object? value, string? config, bool isOutbound)
    {
        if (value == null) return null;

        try
        {
            var boolConfig = !string.IsNullOrEmpty(config)
                ? JsonSerializer.Deserialize<BooleanConfig>(config)
                : null;

            if (isOutbound)
            {
                // Convert AuthScape bool to CRM representation
                var boolValue = Convert.ToBoolean(value);
                if (boolConfig?.CrmTrueValue != null && boolConfig?.CrmFalseValue != null)
                {
                    return boolValue ? boolConfig.CrmTrueValue : boolConfig.CrmFalseValue;
                }
                return boolValue;
            }
            else
            {
                // Convert CRM value to AuthScape bool
                var stringValue = value.ToString()?.ToLowerInvariant();
                if (boolConfig?.CrmTrueValue != null)
                {
                    return stringValue == boolConfig.CrmTrueValue.ToLowerInvariant();
                }
                return stringValue == "true" || stringValue == "1" || stringValue == "yes";
            }
        }
        catch
        {
            return value;
        }
    }

    #endregion

    #region Default Field Mappings

    private IEnumerable<CrmFieldMapping> GetDefaultUserMappings(CrmProviderType providerType)
    {
        return providerType switch
        {
            CrmProviderType.Dynamics365 => new List<CrmFieldMapping>
            {
                new() { AuthScapeField = "FirstName", CrmField = "firstname", SyncDirection = CrmSyncDirection.Bidirectional, IsEnabled = true },
                new() { AuthScapeField = "LastName", CrmField = "lastname", SyncDirection = CrmSyncDirection.Bidirectional, IsEnabled = true },
                new() { AuthScapeField = "Email", CrmField = "emailaddress1", SyncDirection = CrmSyncDirection.Bidirectional, IsEnabled = true },
                new() { AuthScapeField = "PhoneNumber", CrmField = "telephone1", SyncDirection = CrmSyncDirection.Bidirectional, IsEnabled = true },
                new() { AuthScapeField = "Title", CrmField = "jobtitle", SyncDirection = CrmSyncDirection.Bidirectional, IsEnabled = true },
            },
            CrmProviderType.HubSpot => new List<CrmFieldMapping>
            {
                new() { AuthScapeField = "FirstName", CrmField = "firstname", SyncDirection = CrmSyncDirection.Bidirectional, IsEnabled = true },
                new() { AuthScapeField = "LastName", CrmField = "lastname", SyncDirection = CrmSyncDirection.Bidirectional, IsEnabled = true },
                new() { AuthScapeField = "Email", CrmField = "email", SyncDirection = CrmSyncDirection.Bidirectional, IsEnabled = true },
                new() { AuthScapeField = "PhoneNumber", CrmField = "phone", SyncDirection = CrmSyncDirection.Bidirectional, IsEnabled = true },
            },
            _ => new List<CrmFieldMapping>
            {
                new() { AuthScapeField = "FirstName", CrmField = "first_name", SyncDirection = CrmSyncDirection.Bidirectional, IsEnabled = true },
                new() { AuthScapeField = "LastName", CrmField = "last_name", SyncDirection = CrmSyncDirection.Bidirectional, IsEnabled = true },
                new() { AuthScapeField = "Email", CrmField = "email", SyncDirection = CrmSyncDirection.Bidirectional, IsEnabled = true },
            }
        };
    }

    private IEnumerable<CrmFieldMapping> GetDefaultCompanyMappings(CrmProviderType providerType)
    {
        return providerType switch
        {
            CrmProviderType.Dynamics365 => new List<CrmFieldMapping>
            {
                new() { AuthScapeField = "Title", CrmField = "name", SyncDirection = CrmSyncDirection.Bidirectional, IsEnabled = true },
                new() { AuthScapeField = "Description", CrmField = "description", SyncDirection = CrmSyncDirection.Bidirectional, IsEnabled = true },
                new() { AuthScapeField = "Website", CrmField = "websiteurl", SyncDirection = CrmSyncDirection.Bidirectional, IsEnabled = true },
            },
            CrmProviderType.HubSpot => new List<CrmFieldMapping>
            {
                new() { AuthScapeField = "Title", CrmField = "name", SyncDirection = CrmSyncDirection.Bidirectional, IsEnabled = true },
                new() { AuthScapeField = "Description", CrmField = "description", SyncDirection = CrmSyncDirection.Bidirectional, IsEnabled = true },
                new() { AuthScapeField = "Website", CrmField = "website", SyncDirection = CrmSyncDirection.Bidirectional, IsEnabled = true },
            },
            _ => new List<CrmFieldMapping>
            {
                new() { AuthScapeField = "Title", CrmField = "name", SyncDirection = CrmSyncDirection.Bidirectional, IsEnabled = true },
            }
        };
    }

    private IEnumerable<CrmFieldMapping> GetDefaultLocationMappings(CrmProviderType providerType)
    {
        return providerType switch
        {
            CrmProviderType.Dynamics365 => new List<CrmFieldMapping>
            {
                new() { AuthScapeField = "Title", CrmField = "name", SyncDirection = CrmSyncDirection.Bidirectional, IsEnabled = true },
                new() { AuthScapeField = "Address", CrmField = "address1_line1", SyncDirection = CrmSyncDirection.Bidirectional, IsEnabled = true },
                new() { AuthScapeField = "City", CrmField = "address1_city", SyncDirection = CrmSyncDirection.Bidirectional, IsEnabled = true },
                new() { AuthScapeField = "State", CrmField = "address1_stateorprovince", SyncDirection = CrmSyncDirection.Bidirectional, IsEnabled = true },
                new() { AuthScapeField = "ZipCode", CrmField = "address1_postalcode", SyncDirection = CrmSyncDirection.Bidirectional, IsEnabled = true },
            },
            _ => new List<CrmFieldMapping>
            {
                new() { AuthScapeField = "Title", CrmField = "name", SyncDirection = CrmSyncDirection.Bidirectional, IsEnabled = true },
                new() { AuthScapeField = "Address", CrmField = "address", SyncDirection = CrmSyncDirection.Bidirectional, IsEnabled = true },
                new() { AuthScapeField = "City", CrmField = "city", SyncDirection = CrmSyncDirection.Bidirectional, IsEnabled = true },
            }
        };
    }

    private IEnumerable<string> GetUserFields()
    {
        return new[]
        {
            "FirstName", "LastName", "Email", "PhoneNumber", "Title",
            "PhotoUri", "UserName", "CustomFields.*"
        };
    }

    private IEnumerable<string> GetCompanyFields()
    {
        return new[]
        {
            "Title", "Logo", "Description", "Website", "CustomFields.*"
        };
    }

    private IEnumerable<string> GetLocationFields()
    {
        return new[]
        {
            "Title", "Address", "City", "State", "ZipCode",
            "Lat", "Lng", "CustomFields.*"
        };
    }

    #endregion

    #region Transformation Config Classes

    private class DateFormatConfig
    {
        public string? AuthScapeFormat { get; set; }
        public string? CrmFormat { get; set; }
    }

    private class ConcatConfig
    {
        public string? Prefix { get; set; }
        public string? Suffix { get; set; }
    }

    private class SplitConfig
    {
        public string? Delimiter { get; set; }
        public int? Index { get; set; }
    }

    private class DefaultValueConfig
    {
        public object? Value { get; set; }
    }

    private class BooleanConfig
    {
        public string? CrmTrueValue { get; set; }
        public string? CrmFalseValue { get; set; }
    }

    #endregion
}

/// <summary>
/// Interface for entities that have custom fields
/// </summary>
public interface IHasCustomFields
{
    string? CustomFields { get; set; }
}
