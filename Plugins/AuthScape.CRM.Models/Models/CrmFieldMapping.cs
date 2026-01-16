using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AuthScape.CRM.Models.Enums;

namespace AuthScape.CRM.Models;

/// <summary>
/// Configures how individual fields map between AuthScape and CRM entities.
/// For example: AuthScape "FirstName" ↔ Dynamics "firstname"
///              AuthScape "Email" ↔ Dynamics "emailaddress1"
///              AuthScape "CustomFields.Industry" ↔ Dynamics "new_industry"
/// </summary>
public class CrmFieldMapping
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    /// <summary>
    /// The entity mapping this field mapping belongs to
    /// </summary>
    [Required]
    public long CrmEntityMappingId { get; set; }

    /// <summary>
    /// The AuthScape field path (e.g., "FirstName", "Email", "CustomFields.Industry")
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string AuthScapeField { get; set; } = string.Empty;

    /// <summary>
    /// The CRM field name (e.g., "firstname", "emailaddress1", "new_industry")
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string CrmField { get; set; } = string.Empty;

    /// <summary>
    /// Override sync direction for this specific field
    /// </summary>
    public CrmSyncDirection SyncDirection { get; set; } = CrmSyncDirection.Bidirectional;

    /// <summary>
    /// Whether this field mapping is active
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Whether this field is required for sync (record skipped if empty)
    /// </summary>
    public bool IsRequired { get; set; } = false;

    /// <summary>
    /// Optional transformation type to apply during sync
    /// </summary>
    [MaxLength(50)]
    public string? TransformationType { get; set; }

    /// <summary>
    /// JSON configuration for the transformation (depends on TransformationType)
    /// </summary>
    public string? TransformationConfig { get; set; }

    /// <summary>
    /// Display order for UI presentation
    /// </summary>
    public int DisplayOrder { get; set; } = 0;

    // Navigation properties
    [ForeignKey(nameof(CrmEntityMappingId))]
    public virtual CrmEntityMapping? CrmEntityMapping { get; set; }
}

/// <summary>
/// Common transformation types for field mapping
/// </summary>
public static class FieldTransformationType
{
    public const string None = "None";
    public const string Uppercase = "Uppercase";
    public const string Lowercase = "Lowercase";
    public const string TitleCase = "TitleCase";
    public const string DateFormat = "DateFormat";
    public const string NumberFormat = "NumberFormat";
    public const string Lookup = "Lookup";        // Map to/from lookup/reference field
    public const string OptionSet = "OptionSet";  // Map to/from picklist/enum
    public const string Concatenate = "Concatenate";
    public const string Split = "Split";
    public const string CustomScript = "CustomScript";
}
