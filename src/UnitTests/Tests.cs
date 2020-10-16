namespace UnitTests
{
    using DataReaderParser;
    using MoreLinq;
    using NUnit.Framework;
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;

    [TestFixture]
    public class Tests
    {
        struct EmptyStruct
        {
        }

        class OneStruct
        {
            public int A { get; set; }
        }

        class Empty
        {
        }

        class One
        {
            public int A { get; set; }
        }

        class Two
        {
            public int A { get; set; }
            public int B { get; set; }
        }

        class NotNull
        {
            public int A { get; set; }
        }

        class Null
        {
            public int? A { get; set; }
        }

        class String
        {
            public string A { get; set; }
        }

        class Short
        {
            public short A { get; set; }
        }

        class Long
        {
            public long A { get; set; }
        }

        class UInt
        {
            public uint A { get; set; }
        }

        class Int
        {
            public int A { get; set; }
        }

        class Different
        {
            public int A { get; set; }
            public string B { get; set; }
            public DateTime? C { get; set; }
        }

        IEnumerable<T> Parse<T>(IDataReader reader)
            where T : new() => new StaticParser().Parse(reader, () => new T());

        IEnumerable<T> Many<T>(int count, T obj) =>
            Enumerable.Repeat(obj, count);

        void Check<T>(DataReader set, IEnumerable<T> expected)
            where T : new() => AssertStructural.AreCollectionsEqual(Parse<T>(set), expected);

        void Throws<T>(DataReader set) where T : new()
        {
            try
            {
                new StaticParser().Parse(set, () => new T()).Consume();
            }
            catch
            {
                return;
            }

            Assert.Fail();
        }

        // tc={} bc={} br={}
        // tc={} bc={} br={.}
        // tc={} bc={} br={.,.}
        [TestCase(0), TestCase(1), TestCase(2)]
        public void Tc_Bc(int rows)
        {
            var set = new DataReader();
            set.Set.Data.AddRange(Many(rows, new object[] { }));

            Check(set, Many(rows, new Empty { }));
        }

        // tc={} bc={A} br={}
        // tc={} bc={A} br={.}
        // tc={} bc={A} br={.,.}
        [TestCase(0), TestCase(1), TestCase(2)]
        public void Tc_BcA(int rows)
        {
            var set = new DataReader();
            set.AddColumn<int>("A");
            set.AddRows(Many(rows, new object[] { }));

            Check(set, Many(rows, new Empty { }));
        }

        // tc={A} bc={} br={}
        // tc={A} bc={} br={.}
        // tc={A} bc={} br={.,.}
        [TestCase(0), TestCase(1), TestCase(2)]
        public void TcA_Bc(int rows)
        {
            var set = new DataReader();
            set.AddRows(Many(rows, new object[] { }));

            Throws<One>(set);
        }

        // tc={A} bc={A} br={}
        // tc={A} bc={A} br={.}
        // tc={A} bc={A} br={.,.}
        [TestCase(0), TestCase(1), TestCase(2)]
        public void TcA_BcA(int rows)
        {
            var set = new DataReader();
            set.AddColumn<int>("A");
            set.AddRows(Many(rows, new object[] { 1 }));

            Check(set, Many(rows, new One { A = 1 }));
        }

        // tc={A,B} bc={A} br={}
        // tc={A,B} bc={A} br={.}
        // tc={A,B} bc={A} br={.,.}
        [TestCase(0), TestCase(1), TestCase(2)]
        public void TcAB_BcA(int rows)
        {
            var set = new DataReader();
            set.AddColumn<int>("A");
            set.AddRows(Many(rows, new object[] { 1 }));

            Throws<Two>(set);
        }


        // tc={A} bc={A,B} br={}
        // tc={A} bc={A,B} br={.}
        // tc={A} bc={A,B} br={.,.}
        [TestCase(0), TestCase(1), TestCase(2)]
        public void TcA_BcAB(int rows)
        {
            var set = new DataReader();
            set.AddColumn<int>("A");
            set.AddColumn<int>("B");
            set.AddRows(Many(rows, new object[] { 1, 2 }));

            Check(set, Many(rows, new One { A = 1 }));
        }

        // tc={A,B} bc={A,B} br={}
        // tc={A,B} bc={A,B} br={.}
        // tc={A,B} bc={A,B} br={.,.}
        [TestCase(0), TestCase(1), TestCase(2)]
        public void TcAB_BcAB(int rows)
        {
            var set = new DataReader();
            set.AddColumn<int>("A");
            set.AddColumn<int>("B");
            set.AddRows(Many(rows, new object[] { 1, 2 }));

            Check(set, Many(rows, new Two { A = 1, B = 2 }));
        }

        // tc={B} bc={A} br={}
        // tc={B} bc={A} br={.}
        // tc={B} bc={A} br={.,.}
        [TestCase(0), TestCase(1), TestCase(2)]
        public void TcB_BcA(int rows)
        {
            var set = new DataReader();
            set.AddColumn<int>("B");
            set.AddRows(Many(rows, new object[] { 2 }));

            Throws<One>(set);
        }

        // tr<-{{1}} br<-{{1,!}} br={{1}}
        [Test]
        public void Tre1BreN1Br1()
        {
            var set = new DataReader();
            set.AddColumn<int?>("A");
            set.AddRow(new object[] { (int?)1 });

            Check(set, new NotNull { A = 1 }.YieldOne());
        }

        // tr<-{{1}} br<-{{1,!}} br={{!}}
        [Test]
        public void Tre1BreN1BrN()
        {
            var set = new DataReader();
            set.AddColumn<int?>("A");
            set.AddRow(new object[] { null });

            Throws<NotNull>(set);
        }

        // tr<-{{1}} br<-{{1,!}} br={}
        [Test]
        public void Tre1BreN1Br()
        {
            var set = new DataReader();
            set.AddColumn<int?>("A");

            Check(set, new NotNull[] { });
        }

        // tr<-{{1}} bc<-{}
        [Test]
        public void Tre1Bc()
        {
            var set = new DataReader();

            Throws<NotNull>(set);
        }

        // tr<-{{1,!}} br<-{{1,!}} br={{1}}
        [Test]
        public void TreN1BreN1Br1()
        {
            var set = new DataReader();
            set.AddColumn<int?>("A");
            set.AddRow(new object[] { (int?)1 });

            Check(set, new Null { A = 1 }.YieldOne());
        }

        // tr<-{{1,!}} br<-{{1,!}} br={{!}}
        [Test]
        public void TreN1BreN1BrN()
        {
            var set = new DataReader();
            set.AddColumn<int?>("A");
            set.AddRow(new object[] { null });

            Check(set, new Null { A = 1 }.YieldOne());
        }

        // tr<-{{1,!}} br<-{{1,!}} br={}
        [Test]
        public void TreN1BreN1Br()
        {
            var set = new DataReader();
            set.AddColumn<int?>("A");

            Check(set, new Null[] { });
        }

        // tr<-{{1,!}} bc<-{}
        [Test]
        public void TreN1Bc()
        {
            var set = new DataReader();

            Throws<Null>(set);
        }

        // tr<-{{string}} br<-{{string}} br={{"string"}}
        [Test]
        public void TcS_BreS_BrS()
        {
            var set = new DataReader();
            set.AddColumn<string>("A");
            set.AddRow(new object[] { "string" });

            Check(set, new String { A = "string" }.YieldOne());
        }

        // tr<-{{string}} br<-{{string}} br={{""}}
        [Test]
        public void TcS_BreS_BrE()
        {
            var set = new DataReader();
            set.AddColumn<string>("A");
            set.AddRow(new object[] { "" });

            Check(set, new String { A = "" }.YieldOne());
        }

        // tr<-{{string}} br<-{{string}} br={{null}}
        [Test]
        public void TcS_BreS_BrN()
        {
            var set = new DataReader();
            set.AddColumn<string>("A");
            set.AddRow(new object[] { null });

            Check(set, new String { A = null }.YieldOne());
        }

        // tr<-{{string}} br<-{{string}} br={}
        [Test]
        public void TcS_BreS_Br()
        {
            var set = new DataReader();
            set.AddColumn<string>("A");

            Check(set, new String[] { });
        }

        // tr<-{{string}} bc<-{}
        [Test]
        public void TcS_Bc()
        {
            var set = new DataReader();

            Throws<String>(set);
        }

        // tr<-{{1}} br<-{{2}}
        [Test]
        public void Tre1_Bre2()
        {
            var set = new DataReader();
            set.AddColumn<int>("A");

            Throws<String>(set);
        }

        // tr<-{{1}} br<-{{1,2}} br=1
        [Test]
        public void Tre1_Bre12_Br1()
        {
            var set = new DataReader();
            set.AddColumn<int>("A");
            set.AddRow(new object[] { (int)1 });

            Check(set, new Short { A = 1 }.YieldOne());
        }

        // tr<-{{1}} br<-{{1,2}} br=2
        [Test]
        public void Tre1_Bre12_Br2()
        {
            var set = new DataReader();
            set.AddColumn<int>("A");
            set.AddRow(new object[] { int.MaxValue });

            Throws<Short>(set);
        }

        // tr<-{{1,2}} br<-{{1}}
        [Test]
        public void Tre12_Bre1()
        {
            var set = new DataReader();
            set.AddColumn<int>("A");
            set.AddRow(new object[] { (int)1 });

            Check(set, new Long { A = 1 }.YieldOne());
        }

        // tr<-{{1},{1}} br<-{{1},{2}}
        [Test]
        public void Tre11_Bre12()
        {
            var set = new DataReader();
            set.AddColumn<int>("A");
            set.AddColumn<string>("B");
            set.AddRow(new object[] { (int)1, "" });

            Throws<Two>(set);
        }

        // tc<-{A} bc<-{A,A} br<-{{1},{1}}
        [Test]
        public void TcA_BcAA_Br11()
        {
            var set = new DataReader();
            set.AddColumn<int>("A");
            set.AddColumn<int>("A");
            set.AddRow(new object[] { (int)1, (int)2 });

            Throws<One>(set);
        }

        // tc<-{A} bc<-{A,A} br<-{{1},{2}}
        [Test]
        public void TcA_BcAA_Br12()
        {
            var set = new DataReader();
            set.AddColumn<int>("A");
            set.AddColumn<string>("A");
            set.AddRow(new object[] { (int)1, "" });

            Throws<One>(set);
        }

        [Test]
        public void StructEmpty()
        {
            var set = new DataReader();

            Check(set, new EmptyStruct { }.YieldOne());
        }

        [Test]
        public void StructOne()
        {
            var set = new DataReader();
            set.AddColumn<int>("A");
            set.AddRow(new object[] { (int)1 });

            Check(set, new OneStruct { A = 1 }.YieldOne());
        }

        [Test]
        public void Complex()
        {
            var set = new DataReader();
            set.AddColumn<int>("A");
            set.AddColumn<string>("B");
            set.AddColumn<DateTime?>("C");
            set.AddRow(new object[] { (int)1, "different", null });
            set.AddRow(new object[] { (int)2, null, (DateTime?)new DateTime(2000, 01, 05) });

            var expected = new[]
            {
                new Different { A = 1, B = "different", C = null },
                new Different { A = 2, B = null, C = new DateTime(2000, 01, 05) },
            };

            Check(set, expected);
        }

        // signed/unsigned
        [Test]
        public void Signedness_S_Us()
        {
            var set = new DataReader();
            set.AddColumn<int>("A");
            set.AddRow(new object[] { (int)1 });

            Check(set, new UInt { A = 1 }.YieldOne());
        }

        [Test]
        public void Signedness_Us_S()
        {
            var set = new DataReader();
            set.AddColumn<uint>("A");
            set.AddRow(new object[] { (uint)1 });

            Check(set, new Int { A = 1 }.YieldOne());
        }

        // overflows
        [Test]
        public void Signedness_Overflow_S_Us()
        {
            var set = new DataReader();
            set.AddColumn<int>("A");
            set.AddRow(new object[] { int.MinValue });

            Throws<UInt>(set);
        }

        [Test]
        public void Signedness_Overflow_Us_S()
        {
            var set = new DataReader();
            set.AddColumn<uint>("A");
            set.AddRow(new object[] { uint.MaxValue });

            Throws<Int>(set);
        }
    }
}
