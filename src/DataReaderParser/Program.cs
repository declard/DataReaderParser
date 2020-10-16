using System;
using System.Data.SqlClient;

namespace DataReaderParser
{
    class Program
    {
        static void Main(string[] args)
        {
            var connection = new SqlConnection("Data Source=:memory:");
            connection.Open();
            var command = new SqlCommand("select * from orm5", connection);
            var reader = command.ExecuteReader();
            
            var result = new StaticParser().Parse(reader, () => new Dto());

            foreach (var dto in result)
                Console.WriteLine(dto);

            connection.Close();

            Console.ReadLine();
        }

        class Dto
        {
            public int A { get; set; }
            public int? B { get; set; }
            public short? C { get; set; }
            public string D { get; set; }

            public override string ToString() => (A, B, C, D).ToString();
        }
    }
}
