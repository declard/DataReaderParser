namespace DataReaderParser
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Reflection;

    public class ReaderColumnsMetadata
    {
        private Dictionary<string, int> Indexes;

        private ReaderColumnsMetadata()
        {
        }

        // todo should it match by name+type?
        public static ReaderColumnsMetadata Create(IDataReader reader)
        {
            var indexes = new Dictionary<string, int>(reader.FieldCount);

            for (var i = 0; i < reader.FieldCount; i++)
            {
                indexes.Add(reader.GetName(i), i);
            }

            return new ReaderColumnsMetadata { Indexes = indexes };
        }

        public int FindColumn(PropertyInfo prop)
        {
            if (!Indexes.TryGetValue(prop.Name, out var index))
                throw new Exception($"No column found for prop {prop.DeclaringType.Name}.{prop.Name}"); // todo add custom naming

            return index;
        }
    }

}
