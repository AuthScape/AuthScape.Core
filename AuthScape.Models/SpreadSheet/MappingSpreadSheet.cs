namespace AuthScape.Models.SpreadSheet
{
    public class MappingSpreadSheet
    {
        public string NewName { get; set; }
        public string Field { get; set; }
        public bool IsRequired { get; set; }
    }

    public enum DataType
    {
        String = 0,
        Int = 1,
        Decimal = 2,

    }
}
