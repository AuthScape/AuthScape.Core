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

        SortedDocValuesField // Used for faceting, sorting, and grouping on single-valued fields.
    }
}
