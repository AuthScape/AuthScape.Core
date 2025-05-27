using AuthScape.UserManagementSystem.Models;
using Microsoft.EntityFrameworkCore;
using Services.Context;

namespace AuthScape.ContentManagement.Services
{
    public interface ICustomFieldService
    {
        Task<string?> GetUserTextFieldByUser(long userId, string customFieldName);
        Task<string?> GetUserTextFieldByUser(long userId, Guid customFieldId);
        Task<string?> GetUserMultiLineTextFieldByUser(long userId, string customFieldName);
        Task<string?> GetUserMultiLineTextFieldByUser(long userId, Guid customFieldId);
        Task<string?> GetUserNumberFieldByUser(long userId, string customFieldName);
        Task<string?> GetUserNumberFieldByUser(long userId, Guid customFieldId);
        Task<DateTime?> GetUserDateFieldByUser(long userId, string customFieldName);
        Task<DateTime?> GetUserDateFieldByUser(long userId, Guid customFieldId);
        Task<bool?> GetUserBooleanFieldByUser(long userId, string customFieldName);
        Task<bool?> GetUserBooleanFieldByUser(long userId, Guid customFieldId);
        Task<string?> GetUserImageFieldByUser(long userId, string customFieldName);
        Task<string?> GetUserImageFieldByUser(long userId, Guid customFieldId);
        Task<string?> GetUserDropdownFieldByUser(long userId, string customFieldName);
        Task<string?> GetUserDropdownFieldByUser(long userId, Guid customFieldId);



        Task<string?> GetLocationTextFieldByUser(long locationId, string customFieldName);
        Task<string?> GetLocationTextFieldByUser(long locationId, Guid customFieldId);
        Task<string?> GetLocationMultiLineTextFieldByUser(long locationId, string customFieldName);
        Task<string?> GetLocationMultiLineTextFieldByUser(long locationId, Guid customFieldId);
        Task<string?> GetLocationNumberFieldByUser(long locationId, string customFieldName);
        Task<string?> GetLocationNumberFieldByUser(long locationId, Guid customFieldId);
        Task<DateTime?> GetLocationDateFieldByUser(long locationId, string customFieldName);
        Task<DateTime?> GetLocationDateFieldByUser(long locationId, Guid customFieldId);
        Task<bool?> GetLocationBooleanFieldByUser(long locationId, string customFieldName);
        Task<bool?> GetLocationBooleanFieldByUser(long locationId, Guid customFieldId);
        Task<string?> GetLocationImageFieldByUser(long locationId, string customFieldName);
        Task<string?> GetLocationImageFieldByUser(long locationId, Guid customFieldId);
        Task<string?> GetLocationDropdownFieldByUser(long locationId, string customFieldName);
        Task<string?> GetLocationDropdownFieldByUser(long locationId, Guid customFieldId);


        Task<string?> GetCompanyTextFieldByUser(long companyId, string customFieldName);
        Task<string?> GetCompanyTextFieldByUser(long companyId, Guid customFieldId);
        Task<string?> GetCompanyMultiLineTextFieldByUser(long companyId, string customFieldName);
        Task<string?> GetCompanyMultiLineTextFieldByUser(long companyId, Guid customFieldId);
        Task<string?> GetCompanyNumberFieldByUser(long companyId, string customFieldName);
        Task<string?> GetCompanyNumberFieldByUser(long companyId, Guid customFieldId);
        Task<DateTime?> GetCompanyDateFieldByUser(long companyId, string customFieldName);
        Task<DateTime?> GetCompanyDateFieldByUser(long companyId, Guid customFieldId);
        Task<bool?> GetCompanyBooleanFieldByUser(long companyId, string customFieldName);
        Task<bool?> GetCompanyBooleanFieldByUser(long companyId, Guid customFieldId);
        Task<string?> GetCompanyImageFieldByUser(long companyId, string customFieldName);
        Task<string?> GetCompanyImageFieldByUser(long companyId, Guid customFieldId);
        Task<string?> GetCompanyDropdownFieldByUser(long companyId, string customFieldName);
        Task<string?> GetCompanyDropdownFieldByUser(long companyId, Guid customFieldId);
    }

    public class CustomFieldService : ICustomFieldService
    {
        readonly DatabaseContext databaseContext;
        public CustomFieldService(DatabaseContext databaseContext)
        {
            this.databaseContext = databaseContext;
        }

        #region User Fields

        // TextField by User
        public async Task<string?> GetUserTextFieldByUser(long userId, string customFieldName)
        {
            return await databaseContext.UserCustomFields
                .Include(z => z.CustomField)
                .Where(z => z.UserId == userId && z.CustomField.Name == customFieldName && z.CustomField.FieldType == CustomFieldType.TextField)
                .Select(z => z.Value)
                .FirstOrDefaultAsync();
        }

        public async Task<string?> GetUserTextFieldByUser(long userId, Guid customFieldId)
        {
            return await databaseContext.UserCustomFields
                .Include(z => z.CustomField)
                .Where(z => z.UserId == userId && z.CustomFieldId == customFieldId && z.CustomField.FieldType == CustomFieldType.TextField)
                .Select(z => z.Value)
                .FirstOrDefaultAsync();
        }


        //  MultiLineText by user
        public async Task<string?> GetUserMultiLineTextFieldByUser(long userId, string customFieldName)
        {
            return await databaseContext.UserCustomFields
                .Include(z => z.CustomField)
                .Where(z => z.UserId == userId && z.CustomField.Name == customFieldName && z.CustomField.FieldType == CustomFieldType.MultiLineTextField)
                .Select(z => z.Value)
                .FirstOrDefaultAsync();
        }

        public async Task<string?> GetUserMultiLineTextFieldByUser(long userId, Guid customFieldId)
        {
            return await databaseContext.UserCustomFields
                .Include(z => z.CustomField)
                .Where(z => z.UserId == userId && z.CustomFieldId == customFieldId && z.CustomField.FieldType == CustomFieldType.MultiLineTextField)
                .Select(z => z.Value)
                .FirstOrDefaultAsync();
        }


        // Number by user
        public async Task<string?> GetUserNumberFieldByUser(long userId, string customFieldName)
        {
            return await databaseContext.UserCustomFields
                .Include(z => z.CustomField)
                .Where(z => z.UserId == userId && z.CustomField.Name == customFieldName && z.CustomField.FieldType == CustomFieldType.Number)
                .Select(z => z.Value)
                .FirstOrDefaultAsync();
        }

        public async Task<string?> GetUserNumberFieldByUser(long userId, Guid customFieldId)
        {
            return await databaseContext.UserCustomFields
                .Include(z => z.CustomField)
                .Where(z => z.UserId == userId && z.CustomFieldId == customFieldId && z.CustomField.FieldType == CustomFieldType.Number)
                .Select(z => z.Value)
                .FirstOrDefaultAsync();
        }


        // date by user
        public async Task<DateTime?> GetUserDateFieldByUser(long userId, string customFieldName)
        {
            var date = await databaseContext.UserCustomFields
                .Include(z => z.CustomField)
                .Where(z => z.UserId == userId && z.CustomField.Name == customFieldName && z.CustomField.FieldType == CustomFieldType.Date)
                .Select(z => z.Value)
                .FirstOrDefaultAsync();

            if (date != null)
            {
                try
                {
                    return DateTime.Parse(date);
                }
                catch (Exception) { }
            }

            return null;
        }

        public async Task<DateTime?> GetUserDateFieldByUser(long userId, Guid customFieldId)
        {
            var date = await databaseContext.UserCustomFields
                .Include(z => z.CustomField)
                .Where(z => z.UserId == userId && z.CustomFieldId == customFieldId && z.CustomField.FieldType == CustomFieldType.Date)
                .Select(z => z.Value)
                .FirstOrDefaultAsync();

            if (date != null)
            {
                try
                {
                    return DateTime.Parse(date);
                }
                catch(Exception) { }
            }

            return null;
        }


        // Number by user
        public async Task<bool?> GetUserBooleanFieldByUser(long userId, string customFieldName)
        {
            var val = await databaseContext.UserCustomFields
                .Include(z => z.CustomField)
                .Where(z => z.UserId == userId && z.CustomField.Name == customFieldName && z.CustomField.FieldType == CustomFieldType.Boolean)
                .Select(z => z.Value)
                .FirstOrDefaultAsync();

            if (val != null)
            {
                try
                {
                    return bool.Parse(val);
                }
                catch (Exception) { }
            }

            return null;
        }

        public async Task<bool?> GetUserBooleanFieldByUser(long userId, Guid customFieldId)
        {
            var val = await databaseContext.UserCustomFields
                .Include(z => z.CustomField)
                .Where(z => z.UserId == userId && z.CustomFieldId == customFieldId && z.CustomField.FieldType == CustomFieldType.Boolean)
                .Select(z => z.Value)
                .FirstOrDefaultAsync();

            if (val != null)
            {
                try
                {
                    return bool.Parse(val);
                }
                catch (Exception) { }
            }

            return null;
        }


        // Image by User
        public async Task<string?> GetUserImageFieldByUser(long userId, string customFieldName)
        {
            return await databaseContext.UserCustomFields
                .Include(z => z.CustomField)
                .Where(z => z.UserId == userId && z.CustomField.Name == customFieldName && z.CustomField.FieldType == CustomFieldType.Image)
                .Select(z => z.Value)
                .FirstOrDefaultAsync();
        }

        public async Task<string?> GetUserImageFieldByUser(long userId, Guid customFieldId)
        {
            return await databaseContext.UserCustomFields
                .Include(z => z.CustomField)
                .Where(z => z.UserId == userId && z.CustomFieldId == customFieldId && z.CustomField.FieldType == CustomFieldType.Image)
                .Select(z => z.Value)
                .FirstOrDefaultAsync();
        }


        // Dropdown by User
        public async Task<string?> GetUserDropdownFieldByUser(long userId, string customFieldName)
        {
            return await databaseContext.UserCustomFields
                .Include(z => z.CustomField)
                .Where(z => z.UserId == userId && z.CustomField.Name == customFieldName && z.CustomField.FieldType == CustomFieldType.Dropdown)
                .Select(z => z.Value)
                .FirstOrDefaultAsync();
        }

        public async Task<string?> GetUserDropdownFieldByUser(long userId, Guid customFieldId)
        {
            return await databaseContext.UserCustomFields
                .Include(z => z.CustomField)
                .Where(z => z.UserId == userId && z.CustomFieldId == customFieldId && z.CustomField.FieldType == CustomFieldType.Dropdown)
                .Select(z => z.Value)
                .FirstOrDefaultAsync();
        }

        #endregion


        #region Location Fields

        // TextField by User
        public async Task<string?> GetLocationTextFieldByUser(long locationId, string customFieldName)
        {
            return await databaseContext.LocationCustomFields
                .Include(z => z.CustomField)
                .Where(z => z.LocationId == locationId && z.CustomField.Name == customFieldName && z.CustomField.FieldType == CustomFieldType.TextField)
                .Select(z => z.Value)
                .FirstOrDefaultAsync();
        }

        public async Task<string?> GetLocationTextFieldByUser(long locationId, Guid customFieldId)
        {
            return await databaseContext.LocationCustomFields
                .Include(z => z.CustomField)
                .Where(z => z.LocationId == locationId && z.CustomFieldId == customFieldId && z.CustomField.FieldType == CustomFieldType.TextField)
                .Select(z => z.Value)
                .FirstOrDefaultAsync();
        }


        //  MultiLineText by user
        public async Task<string?> GetLocationMultiLineTextFieldByUser(long locationId, string customFieldName)
        {
            return await databaseContext.LocationCustomFields
                .Include(z => z.CustomField)
                .Where(z => z.LocationId == locationId && z.CustomField.Name == customFieldName && z.CustomField.FieldType == CustomFieldType.MultiLineTextField)
                .Select(z => z.Value)
                .FirstOrDefaultAsync();
        }

        public async Task<string?> GetLocationMultiLineTextFieldByUser(long locationId, Guid customFieldId)
        {
            return await databaseContext.LocationCustomFields
                .Include(z => z.CustomField)
                .Where(z => z.LocationId == locationId && z.CustomFieldId == customFieldId && z.CustomField.FieldType == CustomFieldType.MultiLineTextField)
                .Select(z => z.Value)
                .FirstOrDefaultAsync();
        }


        // Number by user
        public async Task<string?> GetLocationNumberFieldByUser(long locationId, string customFieldName)
        {
            return await databaseContext.LocationCustomFields
                .Include(z => z.CustomField)
                .Where(z => z.LocationId == locationId && z.CustomField.Name == customFieldName && z.CustomField.FieldType == CustomFieldType.Number)
                .Select(z => z.Value)
                .FirstOrDefaultAsync();
        }

        public async Task<string?> GetLocationNumberFieldByUser(long locationId, Guid customFieldId)
        {
            return await databaseContext.LocationCustomFields
                .Include(z => z.CustomField)
                .Where(z => z.LocationId == locationId && z.CustomFieldId == customFieldId && z.CustomField.FieldType == CustomFieldType.Number)
                .Select(z => z.Value)
                .FirstOrDefaultAsync();
        }


        // date by user
        public async Task<DateTime?> GetLocationDateFieldByUser(long locationId, string customFieldName)
        {
            var date = await databaseContext.LocationCustomFields
                .Include(z => z.CustomField)
                .Where(z => z.LocationId == locationId && z.CustomField.Name == customFieldName && z.CustomField.FieldType == CustomFieldType.Date)
                .Select(z => z.Value)
                .FirstOrDefaultAsync();

            if (date != null)
            {
                try
                {
                    return DateTime.Parse(date);
                }
                catch (Exception) { }
            }

            return null;
        }

        public async Task<DateTime?> GetLocationDateFieldByUser(long locationId, Guid customFieldId)
        {
            var date = await databaseContext.LocationCustomFields
                .Include(z => z.CustomField)
                .Where(z => z.LocationId == locationId && z.CustomFieldId == customFieldId && z.CustomField.FieldType == CustomFieldType.Date)
                .Select(z => z.Value)
                .FirstOrDefaultAsync();

            if (date != null)
            {
                try
                {
                    return DateTime.Parse(date);
                }
                catch (Exception) { }
            }

            return null;
        }


        // Number by user
        public async Task<bool?> GetLocationBooleanFieldByUser(long locationId, string customFieldName)
        {
            var val = await databaseContext.LocationCustomFields
                .Include(z => z.CustomField)
                .Where(z => z.LocationId == locationId && z.CustomField.Name == customFieldName && z.CustomField.FieldType == CustomFieldType.Boolean)
                .Select(z => z.Value)
                .FirstOrDefaultAsync();

            if (val != null)
            {
                try
                {
                    return bool.Parse(val);
                }
                catch (Exception) { }
            }

            return null;
        }

        public async Task<bool?> GetLocationBooleanFieldByUser(long locationId, Guid customFieldId)
        {
            var val = await databaseContext.LocationCustomFields
                .Include(z => z.CustomField)
                .Where(z => z.LocationId == locationId && z.CustomFieldId == customFieldId && z.CustomField.FieldType == CustomFieldType.Boolean)
                .Select(z => z.Value)
                .FirstOrDefaultAsync();

            if (val != null)
            {
                try
                {
                    return bool.Parse(val);
                }
                catch (Exception) { }
            }

            return null;
        }


        // Image by User
        public async Task<string?> GetLocationImageFieldByUser(long locationId, string customFieldName)
        {
            return await databaseContext.LocationCustomFields
                .Include(z => z.CustomField)
                .Where(z => z.LocationId == locationId && z.CustomField.Name == customFieldName && z.CustomField.FieldType == CustomFieldType.Image)
                .Select(z => z.Value)
                .FirstOrDefaultAsync();
        }

        public async Task<string?> GetLocationImageFieldByUser(long locationId, Guid customFieldId)
        {
            return await databaseContext.LocationCustomFields
                .Include(z => z.CustomField)
                .Where(z => z.LocationId == locationId && z.CustomFieldId == customFieldId && z.CustomField.FieldType == CustomFieldType.Image)
                .Select(z => z.Value)
                .FirstOrDefaultAsync();
        }


        // Dropdown by User
        public async Task<string?> GetLocationDropdownFieldByUser(long locationId, string customFieldName)
        {
            return await databaseContext.LocationCustomFields
                .Include(z => z.CustomField)
                .Where(z => z.LocationId == locationId && z.CustomField.Name == customFieldName && z.CustomField.FieldType == CustomFieldType.Dropdown)
                .Select(z => z.Value)
                .FirstOrDefaultAsync();
        }

        public async Task<string?> GetLocationDropdownFieldByUser(long locationId, Guid customFieldId)
        {
            return await databaseContext.LocationCustomFields
                .Include(z => z.CustomField)
                .Where(z => z.LocationId == locationId && z.CustomFieldId == customFieldId && z.CustomField.FieldType == CustomFieldType.Dropdown)
                .Select(z => z.Value)
                .FirstOrDefaultAsync();
        }

        #endregion


        #region Company Fields

        // TextField by User
        public async Task<string?> GetCompanyTextFieldByUser(long companyId, string customFieldName)
        {
            return await databaseContext.CompanyCustomFields
                .Include(z => z.CustomField)
                .Where(z => z.CompanyId == companyId && z.CustomField.Name == customFieldName && z.CustomField.FieldType == CustomFieldType.TextField)
                .Select(z => z.Value)
                .FirstOrDefaultAsync();
        }

        public async Task<string?> GetCompanyTextFieldByUser(long companyId, Guid customFieldId)
        {
            return await databaseContext.CompanyCustomFields
                .Include(z => z.CustomField)
                .Where(z => z.CompanyId == companyId && z.CustomFieldId == customFieldId && z.CustomField.FieldType == CustomFieldType.TextField)
                .Select(z => z.Value)
                .FirstOrDefaultAsync();
        }


        //  MultiLineText by user
        public async Task<string?> GetCompanyMultiLineTextFieldByUser(long companyId, string customFieldName)
        {
            return await databaseContext.CompanyCustomFields
                .Include(z => z.CustomField)
                .Where(z => z.CompanyId == companyId && z.CustomField.Name == customFieldName && z.CustomField.FieldType == CustomFieldType.MultiLineTextField)
                .Select(z => z.Value)
                .FirstOrDefaultAsync();
        }

        public async Task<string?> GetCompanyMultiLineTextFieldByUser(long companyId, Guid customFieldId)
        {
            return await databaseContext.CompanyCustomFields
                .Include(z => z.CustomField)
                .Where(z => z.CompanyId == companyId && z.CustomFieldId == customFieldId && z.CustomField.FieldType == CustomFieldType.MultiLineTextField)
                .Select(z => z.Value)
                .FirstOrDefaultAsync();
        }


        // Number by user
        public async Task<string?> GetCompanyNumberFieldByUser(long companyId, string customFieldName)
        {
            return await databaseContext.CompanyCustomFields
                .Include(z => z.CustomField)
                .Where(z => z.CompanyId == companyId && z.CustomField.Name == customFieldName && z.CustomField.FieldType == CustomFieldType.Number)
                .Select(z => z.Value)
                .FirstOrDefaultAsync();
        }

        public async Task<string?> GetCompanyNumberFieldByUser(long companyId, Guid customFieldId)
        {
            return await databaseContext.CompanyCustomFields
                .Include(z => z.CustomField)
                .Where(z => z.CompanyId == companyId && z.CustomFieldId == customFieldId && z.CustomField.FieldType == CustomFieldType.Number)
                .Select(z => z.Value)
                .FirstOrDefaultAsync();
        }


        // date by user
        public async Task<DateTime?> GetCompanyDateFieldByUser(long companyId, string customFieldName)
        {
            var date = await databaseContext.CompanyCustomFields
                .Include(z => z.CustomField)
                .Where(z => z.CompanyId == companyId && z.CustomField.Name == customFieldName && z.CustomField.FieldType == CustomFieldType.Date)
                .Select(z => z.Value)
                .FirstOrDefaultAsync();

            if (date != null)
            {
                try
                {
                    return DateTime.Parse(date);
                }
                catch (Exception) { }
            }

            return null;
        }

        public async Task<DateTime?> GetCompanyDateFieldByUser(long companyId, Guid customFieldId)
        {
            var date = await databaseContext.CompanyCustomFields
                .Include(z => z.CustomField)
                .Where(z => z.CompanyId == companyId && z.CustomFieldId == customFieldId && z.CustomField.FieldType == CustomFieldType.Date)
                .Select(z => z.Value)
                .FirstOrDefaultAsync();

            if (date != null)
            {
                try
                {
                    return DateTime.Parse(date);
                }
                catch (Exception) { }
            }

            return null;
        }


        // Number by user
        public async Task<bool?> GetCompanyBooleanFieldByUser(long companyId, string customFieldName)
        {
            var val = await databaseContext.CompanyCustomFields
                .Include(z => z.CustomField)
                .Where(z => z.CompanyId == companyId && z.CustomField.Name == customFieldName && z.CustomField.FieldType == CustomFieldType.Boolean)
                .Select(z => z.Value)
                .FirstOrDefaultAsync();

            if (val != null)
            {
                try
                {
                    return bool.Parse(val);
                }
                catch (Exception) { }
            }

            return null;
        }

        public async Task<bool?> GetCompanyBooleanFieldByUser(long companyId, Guid customFieldId)
        {
            var val = await databaseContext.CompanyCustomFields
                .Include(z => z.CustomField)
                .Where(z => z.CompanyId == companyId && z.CustomFieldId == customFieldId && z.CustomField.FieldType == CustomFieldType.Boolean)
                .Select(z => z.Value)
                .FirstOrDefaultAsync();

            if (val != null)
            {
                try
                {
                    return bool.Parse(val);
                }
                catch (Exception) { }
            }

            return null;
        }


        // Image by User
        public async Task<string?> GetCompanyImageFieldByUser(long companyId, string customFieldName)
        {
            return await databaseContext.CompanyCustomFields
                .Include(z => z.CustomField)
                .Where(z => z.CompanyId == companyId && z.CustomField.Name == customFieldName && z.CustomField.FieldType == CustomFieldType.Image)
                .Select(z => z.Value)
                .FirstOrDefaultAsync();
        }

        public async Task<string?> GetCompanyImageFieldByUser(long companyId, Guid customFieldId)
        {
            return await databaseContext.CompanyCustomFields
                .Include(z => z.CustomField)
                .Where(z => z.CompanyId == companyId && z.CustomFieldId == customFieldId && z.CustomField.FieldType == CustomFieldType.Image)
                .Select(z => z.Value)
                .FirstOrDefaultAsync();
        }


        // Dropdown by User
        public async Task<string?> GetCompanyDropdownFieldByUser(long companyId, string customFieldName)
        {
            return await databaseContext.CompanyCustomFields
                .Include(z => z.CustomField)
                .Where(z => z.CompanyId == companyId && z.CustomField.Name == customFieldName && z.CustomField.FieldType == CustomFieldType.Dropdown)
                .Select(z => z.Value)
                .FirstOrDefaultAsync();
        }

        public async Task<string?> GetCompanyDropdownFieldByUser(long companyId, Guid customFieldId)
        {
            return await databaseContext.CompanyCustomFields
                .Include(z => z.CustomField)
                .Where(z => z.CompanyId == companyId && z.CustomFieldId == customFieldId && z.CustomField.FieldType == CustomFieldType.Dropdown)
                .Select(z => z.Value)
                .FirstOrDefaultAsync();
        }

        #endregion
    }
}
