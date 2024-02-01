namespace UnitTests
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;

    public class DataReader : IDataReader
    {
        public class DataSet
        {
            public List<(string Name, Type Type)> Columns = new List<(string Name, Type Type)> { };
            public List<object[]> Data = new List<object[]> { };
        }

        public List<DataSet> Sets = new List<DataSet> { new DataSet { } };
        public int CurrentSet = 0;
        public int CurrentRow = -1;

        public DataSet Set => Sets[CurrentSet];
        public object[] Row => Set.Data[CurrentRow];

        public void AddColumn<T>(string name) => Set.Columns.Add((name, typeof(T)));
        public void AddRow(object[] row) => Set.Data.Add(row);
        public void AddRows(IEnumerable<object[]> rows) => Set.Data.AddRange(rows);

        public object this[int i] => Row[i];

        public object this[string name] => Row[GetOrdinal(name)];

        public int Depth => 0;

        public bool IsClosed => false;

        public int RecordsAffected => Sets.Select(set => set.Data.Count).Sum();

        public int FieldCount => Set.Columns.Count;

        public void Close() { }
        public void Dispose() { }

        public bool NextResult()
        {
            if (CurrentSet + 1 >= Sets.Count)
                return false;

            CurrentSet++;
            return true;
        }

        public bool Read()
        {
            if (CurrentRow + 1 >= Set.Data.Count)
                return false;

            CurrentRow++;
            return true;
        }
        
        public long GetBytes(int i, long fieldOffset, byte[] buffer, int bufferoffset, int length) => throw new NotImplementedException();
        public long GetChars(int i, long fieldoffset, char[] buffer, int bufferoffset, int length) => throw new NotImplementedException();

        public string GetDataTypeName(int i) => GetFieldType(i).Name;
        public Type GetFieldType(int i) => Set.Columns[i].Type;

        public string GetName(int i) => Set.Columns[i].Name;
        public int GetOrdinal(string name) => Set.Columns.Select((key, value) => KeyValuePair.Create(value, key)).First(c => c.Value.Name == name).Key;

        public DataTable GetSchemaTable()
        {
            var table = new DataTable();

            foreach (var (name, type) in Set.Columns)
                table.Columns.Add(new DataColumn(name, type));

            return table;
        }

        public IDataReader GetData(int i) => new DataReader { Sets = new List<DataSet> { } };

        public int GetValues(object[] values)
        {
            int copyLength = (values.Length < FieldCount) ? values.Length : FieldCount;
            for (int i = 0; i < copyLength; i++)
            {
                values[i] = GetValue(i);
            }

            return copyLength;
        }

        public object GetValue(int i) => Row[i];
        public bool IsDBNull(int i) => Row[i] == null;

        public bool GetBoolean(int i) => (bool)Row[i];
        public byte GetByte(int i) => (byte)Row[i];
        public char GetChar(int i) => (char)Row[i];
        public DateTime GetDateTime(int i) => (DateTime)Row[i];
        public decimal GetDecimal(int i) => (decimal)Row[i];
        public double GetDouble(int i) => (double)Row[i];
        public float GetFloat(int i) => (float)Row[i];
        public Guid GetGuid(int i) => (Guid)Row[i];
        public short GetInt16(int i) => (short)Row[i];
        public int GetInt32(int i) => (int)Row[i];
        public long GetInt64(int i) => (long)Row[i];
        public string GetString(int i) => (string)Row[i];
    }
}
