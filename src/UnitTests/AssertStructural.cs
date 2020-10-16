namespace UnitTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NUnit.Framework;

    public static class AssertStructural
    {
        public static void AreEqual(object left, object right, string message = "", uint maxDepth = 10)
        {
            if (maxDepth == 0)
                Assert.Fail(Desc("Maximum depth reached"));

            if (ReferenceEquals(left, right)) return;

            string Desc(string m) => message + " -> " + m;

            if (left == null)
                Assert.Fail(Desc("Left object is null"));

            if (right == null)
                Assert.Fail(Desc("Right object is null"));

            var leftType = left.GetType();
            var rightType = right.GetType();

            Type GetCommonType()
            {
                if (leftType.IsAssignableFrom(rightType))
                    return leftType;

                if (rightType.IsAssignableFrom(leftType))
                    return rightType;

                Assert.Fail(Desc($"Incompatible types {leftType.FullName} {rightType.FullName}"));
                throw new NotSupportedException();
            }
            var commonType = GetCommonType();

            if (commonType == typeof(string))
            {
                Assert.AreEqual(left, right, Desc("Strings comparison failed"));
                return;
            }

            if (typeof(System.Collections.IEnumerable).IsAssignableFrom(leftType))
            {
                var le = (left as System.Collections.IEnumerable).GetEnumerator();
                var re = (right as System.Collections.IEnumerable).GetEnumerator();

                int i = 0;
                while (true)
                {
                    var hasLeft = le.MoveNext();
                    var hasRight = re.MoveNext();

                    if (!hasLeft && !hasRight)
                        return;

                    if (hasLeft != hasRight)
                        Assert.Fail(Desc("Lengths differ"));

                    AreEqual(le.Current, re.Current, Desc($"At {i}"), maxDepth - 1);

                    i++;
                }
            }

            if (commonType.IsConstructedGenericType)
            {
                dynamic dleft = left;
                dynamic dright = right;

                var genericTypeDefinition = commonType.GetGenericTypeDefinition();

                if (genericTypeDefinition == typeof(Nullable<>))
                {
                    AreEqual(dleft.HasValue, dright.HasValue, Desc("HasValue"), maxDepth - 1);

                    if ((bool)dleft.HasValue)
                        AreEqual(dleft.Value, dright.Value, Desc("Value"), maxDepth - 1);

                    return;
                }

                if (genericTypeDefinition == typeof(KeyValuePair<,>))
                {
                    AreEqual(dleft.Key, dright.Key, Desc("Key"), maxDepth - 1);
                    AreEqual(dleft.Value, dright.Value, Desc("Value"), maxDepth - 1);
                    return;
                }
            }

            //properties: int, double, DateTime, etc, not class
            if (!commonType.IsClass)
            {
                Assert.AreEqual(left, right, Desc("Structural types comparison failed"));
                return;
            }

            foreach (var property in commonType.GetProperties())
            {
                var leftValue = property.GetValue(left);
                var rightValue = property.GetValue(right);

                AreEqual(leftValue, rightValue, Desc(property.Name), maxDepth - 1);
            }

            foreach (var field in commonType.GetFields())
            {
                var leftValue = field.GetValue(left);
                var rightValue = field.GetValue(right);

                AreEqual(leftValue, rightValue, Desc(field.Name), maxDepth - 1);
            }
        }

        public static void AreCollectionsEqual<T>(IEnumerable<T> left, IEnumerable<T> right, string msg = "")
        {
            var leftList = left.ToList();
            var rightList = right.ToList();

            if (leftList.Count != rightList.Count)
                Assert.Fail(msg + $": Different lengths {leftList.Count} {rightList.Count}");

            foreach (var kvp in leftList.Zip(rightList, Tuple.Create).Select((key, value) => KeyValuePair.Create(value, key)))
            {
                AreEqual(kvp.Value.Item1, kvp.Value.Item2, msg + $": At {kvp.Key}");
            }
        }
    }
}
