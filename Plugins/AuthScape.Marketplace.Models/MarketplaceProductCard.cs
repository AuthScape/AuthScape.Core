namespace AuthScape.Marketplace.Models
{
    public class ProductCardCategory
    {
        public Guid Id { get; set; }
        public long? CompanyId { get; set; }
        public int PlatformId { get; set; }
        public string Name { get; set; }

        public string? ParentName { get; set; }

        public bool IsArray { get; set; }

        public ProductCardCategoryType ProductCardCategoryType { get; set; }

        /// <summary>
        /// For ColorField type: JSON dictionary mapping color names to hex values.
        /// </summary>
        public string? ColorHexMappingJson { get; set; }

        /// <summary>
        /// Display order for the filter in the UI. Lower numbers appear first.
        /// Filters without an order (0) will appear after ordered filters.
        /// </summary>
        public int Order { get; set; } = 0;
        //public ICollection<ProductCardField>? ProductFields { get; set; }
    }

    public enum ProductCardCategoryType
    {
        None,

        StringField, // Indexes the field as a single token, not tokenized. Useful for exact matches.

        TextField, // Tokenizes the field for full-text search. Suitable for general text content.

        Int32Field, // Stores a 32-bit integer for efficient range queries and sorting.

        Int64Field, // Stores a 64-bit integer for larger numerical values.

        SingleField, // Stores a single-precision floating point number.

        DoubleField, // Stores a double-precision floating point number.

        StoredField, // Stores a field value but does not index it, useful for storing raw content.

        BinaryField, // Stores binary data, such as byte arrays.

        SortedSetDocValuesField, // Used for faceting, sorting, and grouping on multi-valued fields.

        SortedDocValuesField, // Used for faceting, sorting, and grouping on single-valued fields.

        ColorField, // Color filter with visual swatch display and optional color picker. Values should be CSS color names or use ColorHexMapping for custom mappings.
    }
}
