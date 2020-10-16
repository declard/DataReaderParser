namespace DataReaderParser
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;

    public static class DynamicParser<T>
    {
        private static readonly Func<IDataReader, T, string> _set;

        static DynamicParser()
        {
            var props = typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.SetProperty);
            var reader = Expression.Parameter(typeof(IDataReader));
            var dto = Expression.Parameter(typeof(T));
            var ret = Expression.Label(typeof(string));
            var getType = typeof(object).GetMethod("GetType");
            var nullable = typeof(Nullable<>);
            var readerIndexer = typeof(IDataRecord)
                .GetProperties()
                .Single(p => p.GetIndexParameters().Select(ip => ip.ParameterType).SequenceEqual(new[] { typeof(string) }));

            var variable = Expression.Parameter(typeof(object));
            var isNull = Expression.Equal(variable, Expression.Constant(DBNull.Value));

            _set = props
                .Where(prop => prop.CanWrite)
                .Select(prop =>
                {
                    var propType = prop.PropertyType;

                    var assignVariable = Expression.Assign(
                        variable,
                        Expression.MakeIndex(
                            reader,
                            readerIndexer,
                            new[] { Expression.Constant(prop.Name) }));

                    var onError = Expression.Return(ret, Expression.Constant(prop.Name));

                    Expression MakePropSetter(Type underlyingPropType) => Expression.Block(
                        Expression.IfThen(
                            Expression.NotEqual(
                                Expression.Call(variable, getType),
                                Expression.Constant(underlyingPropType)),
                            onError),
                            Expression.Assign(
                                Expression.MakeMemberAccess(dto, prop),
                                Expression.Convert(variable, propType)));

                    var isRef = !propType.IsValueType;
                    if (isRef)
                        return Expression.Block(
                            new[] { variable },
                            assignVariable,
                            Expression.IfThen(
                                Expression.Not(isNull),
                                MakePropSetter(propType)));

                    var isNullable = propType.IsGenericType && propType.GetGenericTypeDefinition() == nullable;
                    if (isNullable)
                        return Expression.Block(
                            new[] { variable },
                            assignVariable,
                            Expression.IfThen(
                                Expression.Not(isNull),
                                MakePropSetter(propType.GenericTypeArguments[0])));

                    return Expression.Block(
                        new[] { variable },
                        assignVariable,
                        Expression.IfThen(
                            isNull,
                            onError),
                        MakePropSetter(propType));
                })
                .FeedTo(exprs => Expression.Lambda<Func<IDataReader, T, string>>(Expression.Block(
                    exprs.Append<Expression>(Expression.Label(ret, Expression.Constant(null, typeof(string))))), reader, dto)).Compile();
        }

        public static IEnumerable<T> Run(IDataReader reader, Func<T> ctor)
        {
            while (reader.Read())
            {
                var t = ctor();
                var result = _set(reader, t);
                if (result != null)
                    throw new Exception($"Error parsing {result}");

                yield return t;
            }
        }
    }

}
