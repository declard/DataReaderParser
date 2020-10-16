namespace DataReaderParser
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;

    // todo add class name into exception
    public class StaticParser
    {
        private readonly IReadOnlyDictionary<Type, Expression> WellKnownFieldTypeGetters = new Dictionary<Type, Expression>
        {
            AsFieldGetter((reader, ord) => reader.GetInt64(ord)),
            AsFieldGetter((reader, ord) => reader.GetInt32(ord)),
            AsFieldGetter((reader, ord) => reader.GetInt16(ord)),
            AsFieldGetter((reader, ord) => reader.GetChar(ord)),
            AsFieldGetter((reader, ord) => reader.GetByte(ord)),
            AsFieldGetter((reader, ord) => reader.GetBoolean(ord)),
            AsFieldGetter((reader, ord) => reader.GetDateTime(ord)),
            AsFieldGetter((reader, ord) => reader.GetDecimal(ord)),
            AsFieldGetter((reader, ord) => reader.GetDouble(ord)),
            AsFieldGetter((reader, ord) => reader.GetFloat(ord)),
            AsFieldGetter((reader, ord) => reader.GetGuid(ord)),
        };

        private static KeyValuePair<Type, Expression> AsFieldGetter<ColumnType>(Expression<Func<IDataReader, int, ColumnType>> e) =>
            KeyValuePair.Create(typeof(ColumnType), (Expression)e);

        private static readonly Expression<Func<IDataReader, int, bool>> IsDbNull = (reader, ord) => reader.IsDBNull(ord);
        private static readonly Expression<Func<IDataReader, int, object>> ObjectGetter = (reader, ord) => reader.GetValue(ord);

        //private readonly Dictionary<(Type Sql, Type Dto), Expression> KnownTypeCasts = new Dictionary<(Type Sql, Type Dto), Expression>
        //{
        //    AsCast((byte b) => (short)b),
        //    AsCast((byte b) => (int)b),
        //    AsCast((short b) => (int)b),
        //    AsCast((byte b) => (long)b),
        //    AsCast((short b) => (long)b),
        //    AsCast((int b) => (long)b),
        //};

        //private static KeyValuePair<(Type Sql, Type Dto), Expression> AsCast<ColumnType, PropType>(Expression<Func<ColumnType, PropType>> f) =>
        //    KeyValuePair.Create((typeof(ColumnType), typeof(PropType)), (Expression)f);

        // (reader, ord) -> col?
        private Expression GetFieldGetter(Type colType)
        {
            var typeGetter = WellKnownFieldTypeGetters.TryGetValueOrDefault(colType) ?? ObjectGetter;

            var resultType = colType.IsValueType ? MakeNullable(colType) : colType;

            var readerType = typeof(IDataReader);
            var reader = Expression.Parameter(readerType, "reader");
            var ordType = typeof(int);
            var ord = Expression.Parameter(ordType, "ord");

            var getWithNullCheck = Expression.Condition(
                    Expression.Invoke(IsDbNull, reader, ord),
                    Expression.Default(resultType),
                    Expression.Convert(Expression.Invoke(typeGetter, reader, ord), resultType));

            var lambdaType = typeof(Func<,,>).MakeGenericType(readerType, ordType, resultType);
            return Expression.Lambda(lambdaType, getWithNullCheck, new[] { reader, ord });
        }

        private static bool IsNullable(Type type) =>
            type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);

        private static Type MakeNullable(Type type) =>
            typeof(Nullable<>).MakeGenericType(type);

        //private static Type GetNullableInner(Type type) =>
        //    type.GenericTypeArguments[0];

        private Expression GenerateMapping(string name, Type colType, Type propType)
        {
            if (colType == propType)
            {
                var par = Expression.Parameter(colType, "value");
                var lambdaType = typeof(Func<,>).MakeGenericType(colType, propType);
                return Expression.Lambda(lambdaType, par, new[] { par });
            }

            throw new Exception($"Can't build a cast from {colType.Name} to {propType.Name} (prop {name})");
        }

        private Expression<Func<Exception>> NewException(string message) => () => new Exception(message);

        // col? -> prop
        private Expression GetMapping(string name, Type colType, Type propType)
        {
            if (!propType.IsValueType)
                return GenerateMapping(name, colType, propType);

            var inType = MakeNullable(colType);

            var param = Expression.Parameter(inType, "param");

            var isNullable = IsNullable(propType);

            //var propSearchType = isNullable ? GetNullableInner(propType) : propType;

            //var knownCast = KnownTypeCasts.TryGetValueOrDefault((colType, propSearchType)) ?? GenerateMapping(name, colType, propSearchType);
            //if (knownCast == null)
            //    throw new Exception($"No cast from {colType.Name} to {propType.Name}");

            var onNull = isNullable
                ? (Expression)Expression.Default(propType)
                : Expression.Throw(NewException($"Trying to set null to nonnullable prop {name}").Body, propType);

            //var castLifted = Expression.Convert(Expression.Invoke(knownCast, Expression.Convert(param, colType)), propType);

            var cond = Expression.Condition(
                    Expression.Equal(param, Expression.Default(inType)),
                    onNull,
                    Expression.Convert(param, propType));

            var lambdaType = typeof(Func<,>).MakeGenericType(inType, propType);
            return Expression.Lambda(lambdaType, cond, param);
        }

        // (dto, prop) -> ()
        private Expression GetPropSetter(PropertyInfo prop)
        {
            var param = Expression.Parameter(prop.DeclaringType, "Dto");
            var value = Expression.Parameter(prop.PropertyType, "Value");

            var assign = Expression.Assign(Expression.MakeMemberAccess(param, prop), value);

            var lambdaType = typeof(Action<,>).MakeGenericType(prop.DeclaringType, prop.PropertyType);
            return Expression.Lambda(lambdaType, assign, param, value);
        }

        private static Expression Const<T>(T value) =>
            Expression.Constant(value, typeof(T));

        private PropertyInfo[] GetProps<T>() =>
            typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.SetProperty);

        private static int? TryFindColumn(IDataReader reader, string name)
        {
            try
            {
                return reader.GetOrdinal(name);
            }
            catch (IndexOutOfRangeException)
            {
                return null;
            }
        }

        private int FindColumn(IDataReader reader, PropertyInfo prop) => TryFindColumn(reader, prop.Name)
            ?? throw new Exception($"No column found for prop {prop.Name}"); // todo add custom naming

        private readonly Dictionary<(Type Dto, (int Ord, Type ColType)[] Reader), Delegate> ParserCache = new Dictionary<(Type Dto, (int Ord, Type ColType)[] Reader), Delegate>(); // todo thread-safe

        private Action<T> GenerateParser<T>(IDataReader reader)
        {
            var ret = Expression.Label(typeof(void));
            var dto = Expression.Parameter(typeof(T));

            var parserSignature = GetProps<T>()
                .Where(prop => prop.CanWrite)
                .Select(prop =>
                {
                    var ord = FindColumn(reader, prop);
                    return (Ord: ord, ColType: reader.GetFieldType(ord), Prop: prop);
                }).ToList();

            var cachedParser = ParserCache.TryGetValueOrDefault((typeof(T), parserSignature.Select(sign => (sign.Ord, sign.ColType)).ToArray()));

            if (cachedParser != null)
                return (Action<T>)cachedParser;

            InvocationExpression MakeSetter((int Ord, Type ColType, PropertyInfo Prop) sign)
            {
                var (ord, colType, prop) = sign;
                var propType = prop.PropertyType;

                var fieldGetter = GetFieldGetter(colType);
                var cast = GetMapping(prop.Name, colType, propType);
                var set = GetPropSetter(prop);

                var fieldValue = Expression.Invoke(fieldGetter, Const(reader), Const(ord));
                var propValue = Expression.Invoke(cast, fieldValue);

                return Expression.Invoke(set, dto, propValue);
            }

            var setters = parserSignature.Select(MakeSetter);

            return Expression.Lambda<Action<T>>(Expression.Block(setters), new[] { dto }).Compile();
        }

        public IEnumerable<T> Parse<T>(IDataReader reader, Func<T> ctor)
        {
            System.Diagnostics.Debugger.Break();

            var parser = GenerateParser<T>(reader);

            while (reader.Read())
            {
                var t = ctor();
                parser(t);
                yield return t;
            }
        }
    }
}
