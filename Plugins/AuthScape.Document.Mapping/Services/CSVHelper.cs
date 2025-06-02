using CsvHelper;
using System.Globalization;
using System.Reflection;

namespace AuthScape.Document.Mapping.Services
{
    public class CSVHelper
    {
        public void Execute<T>(Stream stream) where T : new()
        {
            using (var reader = new StreamReader(stream))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                if (csv.Read() && csv.ReadHeader()) // Ensure reading of header
                {
                    var records = new List<Dictionary<string, object>>();

                    while (csv.Read())
                    {
                        var record = new Dictionary<string, object>();
                        foreach (var header in csv.HeaderRecord)
                        {
                            record[header] = csv.GetField(header);
                        }
                        records.Add(record);
                    }


                    foreach (var record in records)
                    {
                        CreateAndAssignProperties<T>(record);
                    }
                }
                else
                {
                    Console.WriteLine("Error: Unable to read the header record.");
                }
            }
        }

        public static T CreateAndAssignProperties<T>(Dictionary<string, object> propertyValues) where T : new()
        {
            T instance = new T();

            foreach (var propertyValue in propertyValues)
            {
                PropertyInfo property = typeof(T).GetProperty(propertyValue.Key);
                if (property != null && property.CanWrite)
                {
                    property.SetValue(instance, propertyValue.Value);
                }
            }

            return instance;
        }
    }
}
