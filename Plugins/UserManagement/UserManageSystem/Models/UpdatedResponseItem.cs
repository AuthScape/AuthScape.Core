﻿namespace AuthScape.UserManageSystem.Models
{
    public class UpdatedResponseItem
    {
        public UpdatedResponseItem(string name, string value)
        {
            Name = name;
            Value = value;
        }

        public string Name { get; private set; }
        public string Value { get; private set; }
    }
}
