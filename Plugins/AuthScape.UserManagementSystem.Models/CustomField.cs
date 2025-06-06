﻿namespace AuthScape.UserManagementSystem.Models
{
    public class CustomField
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public CustomFieldType FieldType { get; set; }
        public CustomFieldPlatformType CustomFieldPlatformType { get; set; } = CustomFieldPlatformType.Users;
        public bool IsRequired { get; set; }
        public int? GridSize { get; set; }

        public Guid? TabId { get; set; }

        public ICollection<UserCustomField> UserCustomFields { get; set; }
        public ICollection<CompanyCustomField> CompanyCustomFields { get; set; }
        public ICollection<LocationCustomField> LocationCustomFields { get; set; }

        public long? CompanyId { get; set; }

        public CustomFieldTab CustomFieldTab { get; set; }

        public bool IsColumnOnDatagrid { get; set; }

        public string? Properties { get; set; }
    }

    public class CustomFieldTab
    {
        public Guid Id { get; set; }
        public string Name { get; set; }

        public long? CompanyId { get; set; }
        public CustomFieldPlatformType PlatformType { get; set; }

        public ICollection<CustomField> CustomFieldTabs { get; set; }
    }

    public enum CustomFieldType
    {
        TextField = 1,
        MultiLineTextField = 2,
        Number = 3,
        Date = 4,
        Boolean = 5,
        Image = 6,
        Dropdown = 7
    }

    public enum CustomFieldPlatformType
    {
        Users = 1,
        Companies = 2,
        Locations = 3
    }
}