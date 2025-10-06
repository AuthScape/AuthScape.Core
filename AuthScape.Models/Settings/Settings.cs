namespace AuthScape.Models.Settings
{
    public class Settings
    {
        public Guid Id { get; set; }
        public int SettingTypeId { get; set; }
        public string Name { get; set; }
        public string Value { get; set; }
    }
}