using AuthScape.UserManageSystem.CRM.Interfaces;
using AuthScape.UserManageSystem.Models.CRM;
using AuthScape.UserManageSystem.Models.CRM.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OpenIddict.Validation.AspNetCore;

namespace AuthScape.UserManageSystem.CRM.Controllers;

/// <summary>
/// API controller for managing CRM entity and field mappings
/// </summary>
[ApiController]
[Route("api/UserManagement/[action]")]
[Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
public class CrmMappingController : ControllerBase
{
    private readonly ICrmSyncService _syncService;
    private readonly ICrmEntityMapper _entityMapper;
    private readonly ICrmProviderFactory _providerFactory;
    private readonly ILogger<CrmMappingController> _logger;

    public CrmMappingController(
        ICrmSyncService syncService,
        ICrmEntityMapper entityMapper,
        ICrmProviderFactory providerFactory,
        ILogger<CrmMappingController> logger)
    {
        _syncService = syncService;
        _entityMapper = entityMapper;
        _providerFactory = providerFactory;
        _logger = logger;
    }

    #region Entity Mappings

    /// <summary>
    /// Gets all entity mappings for a connection
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CrmEntityMappingDto>>> GetCrmEntityMappings([FromQuery] long connectionId)
    {
        var mappings = await _syncService.GetEntityMappingsAsync(connectionId);
        return Ok(mappings.Select(MapToEntityDto));
    }

    /// <summary>
    /// Gets a specific entity mapping
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<CrmEntityMappingDto>> GetCrmEntityMapping(long id)
    {
        var mapping = await _syncService.GetEntityMappingAsync(id);
        if (mapping == null)
            return NotFound();

        return Ok(MapToEntityDto(mapping));
    }

    /// <summary>
    /// Creates a new entity mapping
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<CrmEntityMappingDto>> CreateCrmEntityMapping([FromBody] CreateEntityMappingRequest request)
    {
        var mapping = new CrmEntityMapping
        {
            CrmConnectionId = request.ConnectionId,
            CrmEntityName = request.CrmEntityName,
            CrmEntityDisplayName = request.CrmEntityDisplayName,
            AuthScapeEntityType = request.AuthScapeEntityType,
            SyncDirection = request.SyncDirection,
            IsEnabled = request.IsEnabled ?? true,
            CrmFilterExpression = request.CrmFilterExpression
        };

        var created = await _syncService.CreateEntityMappingAsync(mapping);
        _logger.LogInformation("Created entity mapping {Id}: {CrmEntity} -> {AuthScapeType}",
            created.Id, created.CrmEntityName, created.AuthScapeEntityType);

        return Ok(MapToEntityDto(created));
    }

    /// <summary>
    /// Updates an entity mapping
    /// </summary>
    [HttpPut]
    public async Task<ActionResult<CrmEntityMappingDto>> UpdateCrmEntityMapping(long id, [FromBody] UpdateEntityMappingRequest request)
    {
        var mapping = await _syncService.GetEntityMappingAsync(id);
        if (mapping == null)
            return NotFound();

        if (request.CrmEntityDisplayName != null)
            mapping.CrmEntityDisplayName = request.CrmEntityDisplayName;
        if (request.SyncDirection.HasValue)
            mapping.SyncDirection = request.SyncDirection.Value;
        if (request.IsEnabled.HasValue)
            mapping.IsEnabled = request.IsEnabled.Value;
        if (request.CrmFilterExpression != null)
            mapping.CrmFilterExpression = request.CrmFilterExpression;

        var updated = await _syncService.UpdateEntityMappingAsync(mapping);
        _logger.LogInformation("Updated entity mapping {Id}", id);

        return Ok(MapToEntityDto(updated));
    }

    /// <summary>
    /// Deletes an entity mapping and all its field mappings
    /// </summary>
    [HttpDelete]
    public async Task<IActionResult> DeleteCrmEntityMapping(long id)
    {
        var success = await _syncService.DeleteEntityMappingAsync(id);
        if (!success)
            return NotFound();

        _logger.LogInformation("Deleted entity mapping {Id}", id);
        return Ok();
    }

    /// <summary>
    /// Gets available AuthScape entity types
    /// </summary>
    [HttpGet]
    public ActionResult<IEnumerable<AuthScapeEntityTypeInfo>> GetCrmAuthScapeEntityTypes()
    {
        var types = Enum.GetValues<AuthScapeEntityType>()
            .Select(t => new AuthScapeEntityTypeInfo
            {
                Type = t,
                Name = t.ToString(),
                AvailableFields = _entityMapper.GetAvailableAuthScapeFields(t).ToList()
            });

        return Ok(types);
    }

    #endregion

    #region Field Mappings

    /// <summary>
    /// Gets all field mappings for an entity mapping
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CrmFieldMappingDto>>> GetCrmFieldMappings([FromQuery] long entityMappingId)
    {
        var mappings = await _syncService.GetFieldMappingsAsync(entityMappingId);
        return Ok(mappings.Select(MapToFieldDto));
    }

    /// <summary>
    /// Gets a specific field mapping
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<CrmFieldMappingDto>> GetCrmFieldMapping(long id)
    {
        var mapping = await _syncService.GetFieldMappingAsync(id);
        if (mapping == null)
            return NotFound();

        return Ok(MapToFieldDto(mapping));
    }

    /// <summary>
    /// Creates a new field mapping
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<CrmFieldMappingDto>> CreateCrmFieldMapping([FromBody] CreateFieldMappingRequest request)
    {
        var mapping = new CrmFieldMapping
        {
            CrmEntityMappingId = request.EntityMappingId,
            AuthScapeField = request.AuthScapeField,
            CrmField = request.CrmField,
            SyncDirection = request.SyncDirection,
            IsEnabled = request.IsEnabled ?? true,
            TransformationType = request.TransformationType,
            TransformationConfig = request.TransformationConfig
        };

        var created = await _syncService.CreateFieldMappingAsync(mapping);
        _logger.LogInformation("Created field mapping {Id}: {AuthScapeField} <-> {CrmField}",
            created.Id, created.AuthScapeField, created.CrmField);

        return Ok(MapToFieldDto(created));
    }

    /// <summary>
    /// Creates multiple field mappings at once
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<IEnumerable<CrmFieldMappingDto>>> CreateCrmFieldMappingsBatch([FromBody] CreateFieldMappingsBatchRequest request)
    {
        var mappings = request.Mappings.Select(m => new CrmFieldMapping
        {
            CrmEntityMappingId = request.EntityMappingId,
            AuthScapeField = m.AuthScapeField,
            CrmField = m.CrmField,
            SyncDirection = m.SyncDirection,
            IsEnabled = m.IsEnabled ?? true,
            TransformationType = m.TransformationType,
            TransformationConfig = m.TransformationConfig
        }).ToList();

        var created = new List<CrmFieldMapping>();
        foreach (var mapping in mappings)
        {
            created.Add(await _syncService.CreateFieldMappingAsync(mapping));
        }

        _logger.LogInformation("Created {Count} field mappings for entity mapping {EntityMappingId}",
            created.Count, request.EntityMappingId);

        return Ok(created.Select(MapToFieldDto));
    }

    /// <summary>
    /// Updates a field mapping
    /// </summary>
    [HttpPut]
    public async Task<ActionResult<CrmFieldMappingDto>> UpdateCrmFieldMapping(long id, [FromBody] UpdateFieldMappingRequest request)
    {
        var mapping = await _syncService.GetFieldMappingAsync(id);
        if (mapping == null)
            return NotFound();

        if (request.AuthScapeField != null)
            mapping.AuthScapeField = request.AuthScapeField;
        if (request.CrmField != null)
            mapping.CrmField = request.CrmField;
        if (request.SyncDirection.HasValue)
            mapping.SyncDirection = request.SyncDirection.Value;
        if (request.IsEnabled.HasValue)
            mapping.IsEnabled = request.IsEnabled.Value;
        if (request.TransformationType != null)
            mapping.TransformationType = request.TransformationType;
        if (request.TransformationConfig != null)
            mapping.TransformationConfig = request.TransformationConfig;

        var updated = await _syncService.UpdateFieldMappingAsync(mapping);
        _logger.LogInformation("Updated field mapping {Id}", id);

        return Ok(MapToFieldDto(updated));
    }

    /// <summary>
    /// Deletes a field mapping
    /// </summary>
    [HttpDelete]
    public async Task<IActionResult> DeleteCrmFieldMapping(long id)
    {
        var success = await _syncService.DeleteFieldMappingAsync(id);
        if (!success)
            return NotFound();

        _logger.LogInformation("Deleted field mapping {Id}", id);
        return Ok();
    }

    /// <summary>
    /// Gets default field mappings for an entity type and provider
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CrmFieldMappingDto>>> GetCrmDefaultFieldMappings(
        [FromQuery] long connectionId,
        [FromQuery] AuthScapeEntityType entityType)
    {
        var connection = await _syncService.GetConnectionAsync(connectionId);
        if (connection == null)
            return NotFound();

        var defaults = _entityMapper.GetDefaultFieldMappings(entityType, connection.Provider);
        return Ok(defaults.Select(MapToFieldDto));
    }

    /// <summary>
    /// Gets available transformation types
    /// </summary>
    [HttpGet]
    public ActionResult<IEnumerable<TransformationTypeInfo>> GetCrmTransformationTypes()
    {
        var transformations = new List<TransformationTypeInfo>
        {
            new() { Type = "None", Description = "No transformation" },
            new() { Type = "Uppercase", Description = "Convert to uppercase" },
            new() { Type = "Lowercase", Description = "Convert to lowercase" },
            new() { Type = "Trim", Description = "Remove leading/trailing whitespace" },
            new() { Type = "DateFormat", Description = "Convert date format", RequiresConfig = true, ConfigSchema = "{\"authScapeFormat\":\"string\",\"crmFormat\":\"string\"}" },
            new() { Type = "Lookup", Description = "Map values using a lookup table", RequiresConfig = true, ConfigSchema = "{\"sourceValue\":\"targetValue\"}" },
            new() { Type = "Concat", Description = "Add prefix/suffix", RequiresConfig = true, ConfigSchema = "{\"prefix\":\"string\",\"suffix\":\"string\"}" },
            new() { Type = "Split", Description = "Split and take part", RequiresConfig = true, ConfigSchema = "{\"delimiter\":\"string\",\"index\":\"number\"}" },
            new() { Type = "Default", Description = "Use default value if null", RequiresConfig = true, ConfigSchema = "{\"value\":\"any\"}" },
            new() { Type = "Boolean", Description = "Convert boolean values", RequiresConfig = true, ConfigSchema = "{\"crmTrueValue\":\"string\",\"crmFalseValue\":\"string\"}" }
        };

        return Ok(transformations);
    }

    #endregion

    #region Relationship Mappings

    /// <summary>
    /// Gets all relationship mappings for an entity mapping
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CrmRelationshipMappingDto>>> GetCrmRelationshipMappings([FromQuery] long entityMappingId)
    {
        var entityMapping = await _syncService.GetEntityMappingAsync(entityMappingId);
        if (entityMapping == null)
            return NotFound();

        var mappings = entityMapping.RelationshipMappings ?? new List<CrmRelationshipMapping>();
        return Ok(mappings.Select(MapToRelationshipDto));
    }

    /// <summary>
    /// Gets a specific relationship mapping
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<CrmRelationshipMappingDto>> GetCrmRelationshipMapping(long id)
    {
        var mapping = await _syncService.GetRelationshipMappingAsync(id);
        if (mapping == null)
            return NotFound();

        return Ok(MapToRelationshipDto(mapping));
    }

    /// <summary>
    /// Creates a new relationship mapping
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<CrmRelationshipMappingDto>> CreateCrmRelationshipMapping([FromBody] CreateRelationshipMappingRequest request)
    {
        var mapping = new CrmRelationshipMapping
        {
            CrmEntityMappingId = request.EntityMappingId,
            AuthScapeField = request.AuthScapeField,
            RelatedAuthScapeEntityType = request.RelatedAuthScapeEntityType,
            CrmLookupField = request.CrmLookupField,
            CrmRelatedEntityName = request.CrmRelatedEntityName,
            DisplayName = request.DisplayName,
            SyncDirection = request.SyncDirection,
            IsEnabled = request.IsEnabled ?? true,
            AutoCreateRelated = request.AutoCreateRelated ?? false,
            SyncNullValues = request.SyncNullValues ?? true
        };

        var created = await _syncService.CreateRelationshipMappingAsync(mapping);
        _logger.LogInformation("Created relationship mapping {Id}: {AuthScapeField} -> {CrmLookupField} ({CrmRelatedEntity})",
            created.Id, created.AuthScapeField, created.CrmLookupField, created.CrmRelatedEntityName);

        return Ok(MapToRelationshipDto(created));
    }

    /// <summary>
    /// Updates a relationship mapping
    /// </summary>
    [HttpPut]
    public async Task<ActionResult<CrmRelationshipMappingDto>> UpdateCrmRelationshipMapping(long id, [FromBody] UpdateRelationshipMappingRequest request)
    {
        var mapping = await _syncService.GetRelationshipMappingAsync(id);
        if (mapping == null)
            return NotFound();

        if (request.AuthScapeField != null)
            mapping.AuthScapeField = request.AuthScapeField;
        if (request.RelatedAuthScapeEntityType.HasValue)
            mapping.RelatedAuthScapeEntityType = request.RelatedAuthScapeEntityType.Value;
        if (request.CrmLookupField != null)
            mapping.CrmLookupField = request.CrmLookupField;
        if (request.CrmRelatedEntityName != null)
            mapping.CrmRelatedEntityName = request.CrmRelatedEntityName;
        if (request.DisplayName != null)
            mapping.DisplayName = request.DisplayName;
        if (request.SyncDirection.HasValue)
            mapping.SyncDirection = request.SyncDirection.Value;
        if (request.IsEnabled.HasValue)
            mapping.IsEnabled = request.IsEnabled.Value;
        if (request.AutoCreateRelated.HasValue)
            mapping.AutoCreateRelated = request.AutoCreateRelated.Value;
        if (request.SyncNullValues.HasValue)
            mapping.SyncNullValues = request.SyncNullValues.Value;

        var updated = await _syncService.UpdateRelationshipMappingAsync(mapping);
        _logger.LogInformation("Updated relationship mapping {Id}", id);

        return Ok(MapToRelationshipDto(updated));
    }

    /// <summary>
    /// Deletes a relationship mapping
    /// </summary>
    [HttpDelete]
    public async Task<IActionResult> DeleteCrmRelationshipMapping(long id)
    {
        var success = await _syncService.DeleteRelationshipMappingAsync(id);
        if (!success)
            return NotFound();

        _logger.LogInformation("Deleted relationship mapping {Id}", id);
        return Ok();
    }

    /// <summary>
    /// Gets available relationship fields for an AuthScape entity type
    /// </summary>
    [HttpGet]
    public ActionResult<IEnumerable<RelationshipFieldInfo>> GetCrmRelationshipFields([FromQuery] AuthScapeEntityType entityType)
    {
        var fields = entityType switch
        {
            AuthScapeEntityType.User => new List<RelationshipFieldInfo>
            {
                new() { Field = "CompanyId", DisplayName = "Company", RelatedEntityType = AuthScapeEntityType.Company },
                new() { Field = "LocationId", DisplayName = "Location", RelatedEntityType = AuthScapeEntityType.Location }
            },
            AuthScapeEntityType.Location => new List<RelationshipFieldInfo>
            {
                new() { Field = "CompanyId", DisplayName = "Company", RelatedEntityType = AuthScapeEntityType.Company }
            },
            _ => new List<RelationshipFieldInfo>()
        };

        return Ok(fields);
    }

    #endregion

    #region Private Helpers

    private static CrmEntityMappingDto MapToEntityDto(CrmEntityMapping mapping)
    {
        return new CrmEntityMappingDto
        {
            Id = mapping.Id,
            ConnectionId = mapping.CrmConnectionId,
            CrmEntityName = mapping.CrmEntityName,
            CrmEntityDisplayName = mapping.CrmEntityDisplayName,
            AuthScapeEntityType = mapping.AuthScapeEntityType,
            SyncDirection = mapping.SyncDirection,
            IsEnabled = mapping.IsEnabled,
            CrmFilterExpression = mapping.CrmFilterExpression,
            Created = mapping.Created
        };
    }

    private static CrmFieldMappingDto MapToFieldDto(CrmFieldMapping mapping)
    {
        return new CrmFieldMappingDto
        {
            Id = mapping.Id,
            EntityMappingId = mapping.CrmEntityMappingId,
            AuthScapeField = mapping.AuthScapeField,
            CrmField = mapping.CrmField,
            SyncDirection = mapping.SyncDirection,
            IsEnabled = mapping.IsEnabled,
            TransformationType = mapping.TransformationType,
            TransformationConfig = mapping.TransformationConfig
        };
    }

    private static CrmRelationshipMappingDto MapToRelationshipDto(CrmRelationshipMapping mapping)
    {
        return new CrmRelationshipMappingDto
        {
            Id = mapping.Id,
            EntityMappingId = mapping.CrmEntityMappingId,
            AuthScapeField = mapping.AuthScapeField,
            RelatedAuthScapeEntityType = mapping.RelatedAuthScapeEntityType,
            CrmLookupField = mapping.CrmLookupField,
            CrmRelatedEntityName = mapping.CrmRelatedEntityName,
            DisplayName = mapping.DisplayName,
            SyncDirection = mapping.SyncDirection,
            IsEnabled = mapping.IsEnabled,
            AutoCreateRelated = mapping.AutoCreateRelated,
            SyncNullValues = mapping.SyncNullValues
        };
    }

    #endregion
}

#region DTOs

public class CrmEntityMappingDto
{
    public long Id { get; set; }
    public long ConnectionId { get; set; }
    public string CrmEntityName { get; set; } = string.Empty;
    public string? CrmEntityDisplayName { get; set; }
    public AuthScapeEntityType AuthScapeEntityType { get; set; }
    public CrmSyncDirection SyncDirection { get; set; }
    public bool IsEnabled { get; set; }
    public string? CrmFilterExpression { get; set; }
    public DateTimeOffset Created { get; set; }
}

public class CrmFieldMappingDto
{
    public long Id { get; set; }
    public long EntityMappingId { get; set; }
    public string AuthScapeField { get; set; } = string.Empty;
    public string CrmField { get; set; } = string.Empty;
    public CrmSyncDirection SyncDirection { get; set; }
    public bool IsEnabled { get; set; }
    public string? TransformationType { get; set; }
    public string? TransformationConfig { get; set; }
}

public class AuthScapeEntityTypeInfo
{
    public AuthScapeEntityType Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<string> AvailableFields { get; set; } = new();
}

public class TransformationTypeInfo
{
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool RequiresConfig { get; set; }
    public string? ConfigSchema { get; set; }
}

public class CreateEntityMappingRequest
{
    public long ConnectionId { get; set; }
    public string CrmEntityName { get; set; } = string.Empty;
    public string? CrmEntityDisplayName { get; set; }
    public AuthScapeEntityType AuthScapeEntityType { get; set; }
    public CrmSyncDirection SyncDirection { get; set; } = CrmSyncDirection.Bidirectional;
    public bool? IsEnabled { get; set; }
    public string? CrmFilterExpression { get; set; }
}

public class UpdateEntityMappingRequest
{
    public string? CrmEntityDisplayName { get; set; }
    public CrmSyncDirection? SyncDirection { get; set; }
    public bool? IsEnabled { get; set; }
    public string? CrmFilterExpression { get; set; }
}

public class CreateFieldMappingRequest
{
    public long EntityMappingId { get; set; }
    public string AuthScapeField { get; set; } = string.Empty;
    public string CrmField { get; set; } = string.Empty;
    public CrmSyncDirection SyncDirection { get; set; } = CrmSyncDirection.Bidirectional;
    public bool? IsEnabled { get; set; }
    public string? TransformationType { get; set; }
    public string? TransformationConfig { get; set; }
}

public class CreateFieldMappingsBatchRequest
{
    public long EntityMappingId { get; set; }
    public List<FieldMappingItem> Mappings { get; set; } = new();
}

public class FieldMappingItem
{
    public string AuthScapeField { get; set; } = string.Empty;
    public string CrmField { get; set; } = string.Empty;
    public CrmSyncDirection SyncDirection { get; set; } = CrmSyncDirection.Bidirectional;
    public bool? IsEnabled { get; set; }
    public string? TransformationType { get; set; }
    public string? TransformationConfig { get; set; }
}

public class UpdateFieldMappingRequest
{
    public string? AuthScapeField { get; set; }
    public string? CrmField { get; set; }
    public CrmSyncDirection? SyncDirection { get; set; }
    public bool? IsEnabled { get; set; }
    public string? TransformationType { get; set; }
    public string? TransformationConfig { get; set; }
}

public class CrmRelationshipMappingDto
{
    public long Id { get; set; }
    public long EntityMappingId { get; set; }
    public string AuthScapeField { get; set; } = string.Empty;
    public AuthScapeEntityType RelatedAuthScapeEntityType { get; set; }
    public string CrmLookupField { get; set; } = string.Empty;
    public string CrmRelatedEntityName { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public CrmSyncDirection SyncDirection { get; set; }
    public bool IsEnabled { get; set; }
    public bool AutoCreateRelated { get; set; }
    public bool SyncNullValues { get; set; }
}

public class RelationshipFieldInfo
{
    public string Field { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public AuthScapeEntityType RelatedEntityType { get; set; }
}

public class CreateRelationshipMappingRequest
{
    public long EntityMappingId { get; set; }
    public string AuthScapeField { get; set; } = string.Empty;
    public AuthScapeEntityType RelatedAuthScapeEntityType { get; set; }
    public string CrmLookupField { get; set; } = string.Empty;
    public string CrmRelatedEntityName { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public CrmSyncDirection SyncDirection { get; set; } = CrmSyncDirection.Bidirectional;
    public bool? IsEnabled { get; set; }
    public bool? AutoCreateRelated { get; set; }
    public bool? SyncNullValues { get; set; }
}

public class UpdateRelationshipMappingRequest
{
    public string? AuthScapeField { get; set; }
    public AuthScapeEntityType? RelatedAuthScapeEntityType { get; set; }
    public string? CrmLookupField { get; set; }
    public string? CrmRelatedEntityName { get; set; }
    public string? DisplayName { get; set; }
    public CrmSyncDirection? SyncDirection { get; set; }
    public bool? IsEnabled { get; set; }
    public bool? AutoCreateRelated { get; set; }
    public bool? SyncNullValues { get; set; }
}

#endregion
