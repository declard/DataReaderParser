namespace DataReaderParser
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;

    // todo add specific exception class
    // todo add class name into exception
    public class StaticParser
    {
        private static readonly Expression<Func<IDataReader, int, bool>> IsDbNull = (reader, ord) => reader.IsDBNull(ord);
        private static readonly Expression<Func<IDataReader, int, object>> ObjectGetter = (reader, ord) => reader.GetValue(ord);
        private static readonly Type OpenNullable = typeof(Nullable<>);

        private readonly ConcurrentDictionary<(Type Dto, (int Ord, Type ColType)[] Reader), Delegate> ParserCache = new();
        private readonly ConcurrentDictionary<Type, Expression> GetterCache = new();

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

        // (reader, ord) -> col?
        private Expression GetFieldGetter(Type colType)
        {
            var typeGetter = WellKnownFieldTypeGetters.TryGetValueOrDefault(colType) ?? ObjectGetter;

            var resultType = MakeNullable(colType);

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
            type.IsGenericType && type.GetGenericTypeDefinition() == OpenNullable;

        private static Type MakeNullable(Type type) =>
            IsNullable(type) || !type.IsValueType ? type : OpenNullable.MakeGenericType(type);

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

            var onNull = isNullable
                ? (Expression)Expression.Default(propType)
                : Expression.Throw(NewException($"Trying to set null to nonnullable prop {name}").Body, propType);

            var cond = Expression.Condition(
                    Expression.Equal(param, Expression.Default(inType)),
                    onNull,
                    Expression.ConvertChecked(param, propType));

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

        private Action<IDataReader, T> GenerateParser<T>(IDataReader reader)
        {
            ParameterExpression readerParam = Expression.Parameter(typeof(IDataReader));
            ParameterExpression dtoParam = Expression.Parameter(typeof(T));
            var columns = ReaderColumnsMetadata.Create(reader);

            var parserSignature = GetProps<T>()
                .Where(prop => prop.CanWrite)
                .Select(prop =>
                {
                    var ord = columns.FindColumn(prop);
                    return (Ord: ord, ColType: reader.GetFieldType(ord), Prop: prop);
                }).ToList();

            InvocationExpression MakeSetter((int Ord, Type ColType, PropertyInfo Prop) signature)
            {
                var (ord, colType, prop) = signature;

                var fieldGetter = GetterCache.GetOrAdd(colType, GetFieldGetter);
                var cast = GetMapping(prop.Name, colType, prop.PropertyType);
                var set = GetPropSetter(prop);

                var fieldValue = Expression.Invoke(fieldGetter, readerParam, Const(ord));
                var propValue = Expression.Invoke(cast, fieldValue);
                var setValue = Expression.Invoke(set, dtoParam, propValue);

                return setValue;
            }

            Delegate CreateParser((Type Dto, (int Ord, Type ColType)[] Reader) key)
            {
                var setters = parserSignature.Select(MakeSetter);

                return Expression.Lambda<Action<IDataReader, T>>(Expression.Block(setters), new[] { readerParam, dtoParam }).Compile();
            }

            var key = (typeof(T), parserSignature.Select(signature => (signature.Ord, signature.ColType)).ToArray());

            return (Action<IDataReader, T>)ParserCache.GetOrAdd(key, CreateParser);
        }

        public IEnumerable<T> Parse<T>(IDataReader reader, Func<T> ctor)
        {
            var parser = GenerateParser<T>(reader);

            while (reader.Read())
            {
                var t = ctor();
                parser(reader, t);
                yield return t;
            }
        }
    }
}
