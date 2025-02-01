namespace AuthScape.Marketplace.Models
{
    public class ProductCard
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string? Photo { get; set; }

        public ICollection<ProductCardAndCardFieldMapping> ProductCardAndCardFieldMapping { get; set; }
    }

    public class ProductCardCategory
    {
        public Guid Id { get; set; }
        public string Name { get; set; }

        public ICollection<ProductCardField> ProductFields { get; set; }
    }

    public class ProductCardField
    {
        public Guid Id { get; set; }
        public Guid ProductCategoryId { get; set; }
        public string Name { get; set; }

        public ProductCardCategory ProductCategory { get; set; }
        public ICollection<ProductCardAndCardFieldMapping> ProductCardAndCardFieldMapping { get; set; }
    }

    public class ProductCardAndCardFieldMapping
    {
        public Guid Id { get; set; }
        public Guid ProductFieldId { get; set; }
        public Guid ProductId { get; set; }

        public ProductCard Product { get; set; }
        public ProductCardField ProductField { get; set; }
    }
}
