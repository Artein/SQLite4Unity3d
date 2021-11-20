using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using JetBrains.Annotations;
using SQLite4Unity3d.Utils;

namespace SQLite4Unity3d
{
    public class TableQuery<T> : BaseTableQuery, IEnumerable<T>
    {
        private bool _isDeferredInvocation;
        private BaseTableQuery _joinInner;
        private Expression _joinInnerKeySelectorExpression;
        private BaseTableQuery _joinOuter;
        private Expression _joinOuterKeySelectorExpression;
        private Expression _joinSelectorExpression;
        private int? _limit;
        private int? _offset;
        private List<Ordering> _orderBys;
        private Expression _selectExpression;
        private Expression _whereExpression;

        private TableQuery(SQLiteConnection connection, TableMapping tableMapping)
        {
            Connection = connection;
            TableMapping = tableMapping;
        }

        public TableQuery(SQLiteConnection connection)
        {
            Connection = connection;
            TableMapping = Connection.GetMapping<T>();
        }

        public SQLiteConnection Connection { get; }

        public TableMapping TableMapping { get; }

        public IEnumerator<T> GetEnumerator()
        {
            SQLiteCommand command = BuildCommand("*");
            return _isDeferredInvocation
                ? command.ExecuteDeferredQuery<T>().GetEnumerator()
                : command.ExecuteQuery<T>().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        [PublicAPI]
        public TableQuery<U> Clone<U>()
        {
            var tableQuery = new TableQuery<U>(Connection, TableMapping) { _whereExpression = _whereExpression, _isDeferredInvocation = _isDeferredInvocation };
            if (_orderBys != null)
            {
                tableQuery._orderBys = new List<Ordering>(_orderBys);
            }

            tableQuery._limit = _limit;
            tableQuery._offset = _offset;
            tableQuery._joinInner = _joinInner;
            tableQuery._joinInnerKeySelectorExpression = _joinInnerKeySelectorExpression;
            tableQuery._joinOuter = _joinOuter;
            tableQuery._joinOuterKeySelectorExpression = _joinOuterKeySelectorExpression;
            tableQuery._joinSelectorExpression = _joinSelectorExpression;
            tableQuery._selectExpression = _selectExpression;
            return tableQuery;
        }

        [PublicAPI]
        public TableQuery<T> Where(Expression<Func<T, bool>> expression)
        {
            if (expression.NodeType != ExpressionType.Lambda)
            {
                throw new NotSupportedException("Must be a predicate");
            }

            var lambdaExpression = (LambdaExpression)expression;
            Expression predicateExpression = lambdaExpression.Body;
            var tableQuery = Clone<T>();
            tableQuery.AddWhere(predicateExpression);
            return tableQuery;
        }

        [PublicAPI]
        public TableQuery<T> Take(int number)
        {
            var tableQuery = Clone<T>();
            tableQuery._limit = number;
            return tableQuery;
        }

        [PublicAPI]
        public TableQuery<T> Skip(int number)
        {
            var tableQuery = Clone<T>();
            tableQuery._offset = number;
            return tableQuery;
        }

        [PublicAPI]
        public T ElementAt(int index)
        {
            return Skip(index).Take(1).First();
        }

        [PublicAPI]
        public TableQuery<T> CloneAsDeferred()
        {
            var tableQuery = Clone<T>();
            tableQuery._isDeferredInvocation = true;
            return tableQuery;
        }

        [PublicAPI]
        public TableQuery<T> OrderBy<U>(Expression<Func<T, U>> orderExpression)
        {
            return AddOrderBy(orderExpression, true);
        }

        [PublicAPI]
        public TableQuery<T> OrderByDescending<U>(Expression<Func<T, U>> orderExpression)
        {
            return AddOrderBy(orderExpression, false);
        }

        [PublicAPI]
        public TableQuery<T> ThenBy<U>(Expression<Func<T, U>> orderExpression)
        {
            return AddOrderBy(orderExpression, true);
        }

        [PublicAPI]
        public TableQuery<T> ThenByDescending<U>(Expression<Func<T, U>> orderExpression)
        {
            return AddOrderBy(orderExpression, false);
        }

        private TableQuery<T> AddOrderBy<U>(Expression<Func<T, U>> orderExpression, bool isAscending)
        {
            if (orderExpression.NodeType != ExpressionType.Lambda)
            {
                throw new NotSupportedException("Must be a predicate");
            }

            var lambdaExpression = (LambdaExpression)orderExpression;

            MemberExpression memberExpression;

            if (lambdaExpression.Body is UnaryExpression { NodeType: ExpressionType.Convert } unaryExpression)
            {
                memberExpression = unaryExpression.Operand as MemberExpression;
            }
            else
            {
                memberExpression = lambdaExpression.Body as MemberExpression;
            }

            if (memberExpression == null || memberExpression.Expression.NodeType != ExpressionType.Parameter)
            {
                throw new NotSupportedException("OrderBy does not support: " + orderExpression);
            }

            var newTableQuery = Clone<T>();
            newTableQuery._orderBys ??= new List<Ordering>();
            TableMapping.Column column = TableMapping.FindColumnWithPropertyName(memberExpression.Member.Name);
            newTableQuery._orderBys.Add(new Ordering { ColumnName = column.Name, IsAscending = isAscending });
            return newTableQuery;
        }

        private void AddWhere(Expression predicateExpression)
        {
            _whereExpression = _whereExpression == null ? predicateExpression : Expression.AndAlso(_whereExpression, predicateExpression);
        }

        [PublicAPI]
        public TableQuery<TResult> Join<TInner, TKey, TResult>(
            TableQuery<TInner> inner,
            Expression<Func<T, TKey>> outerKeySelectorExpression,
            Expression<Func<TInner, TKey>> innerKeySelectorExpression,
            Expression<Func<T, TInner, TResult>> joinSelectorExpression)
        {
            var tableQuery = new TableQuery<TResult>(Connection, Connection.GetMapping<TResult>())
            {
                _joinOuter = this,
                _joinOuterKeySelectorExpression = outerKeySelectorExpression,
                _joinInner = inner,
                _joinInnerKeySelectorExpression = innerKeySelectorExpression,
                _joinSelectorExpression = joinSelectorExpression,
            };
            return tableQuery;
        }

        [PublicAPI]
        public TableQuery<TResult> Select<TResult>(Expression<Func<T, TResult>> selectExpression)
        {
            var tableQuery = Clone<TResult>();
            tableQuery._selectExpression = selectExpression;
            return tableQuery;
        }

        private SQLiteCommand BuildCommand(string selectionList)
        {
            if (_joinInner != null && _joinOuter != null)
            {
                throw new NotSupportedException("Joins are not supported.");
            }

            var sqlQuery = $"select {selectionList} from \"{TableMapping.TableName}\"";
            var parameters = new List<object>();
            if (_whereExpression != null)
            {
                CompilationResult result = CompileExpression(_whereExpression, parameters);
                sqlQuery += $" where {result.SqlQuery}";
            }

            if (_orderBys is { Count: > 0 })
            {
                var ordering = _orderBys.Select(o => "\"" + o.ColumnName + "\"" + (o.IsAscending ? "" : " desc")).ToArray();
                sqlQuery += $" order by {string.Join(", ", ordering)}";
            }

            if (_limit.HasValue)
            {
                sqlQuery += " limit " + _limit.Value;
            }

            if (_offset.HasValue)
            {
                if (!_limit.HasValue)
                {
                    sqlQuery += " limit -1 ";
                }

                sqlQuery += " offset " + _offset.Value;
            }

            return Connection.CreateCommand(sqlQuery, parameters.ToArray());
        }

        private CompilationResult CompileExpression(Expression expression, IList<object> queryParameters)
        {
            switch (expression)
            {
                case null:
                    throw new ArgumentNullException(nameof(expression));
                case BinaryExpression binaryExpression:
                {
                    CompilationResult leftResult = CompileExpression(binaryExpression.Left, queryParameters);
                    CompilationResult rightResult = CompileExpression(binaryExpression.Right, queryParameters);

                    // If either side is a parameter and is null, then handle the other side specially (for "is null"/"is not null")
                    string sqlQuery;
                    if (leftResult.SqlQuery == "?" && leftResult.Value == null)
                    {
                        sqlQuery = CompileNullBinaryExpression(binaryExpression, rightResult);
                    }
                    else if (rightResult.SqlQuery == "?" && rightResult.Value == null)
                    {
                        sqlQuery = CompileNullBinaryExpression(binaryExpression, leftResult);
                    }
                    else
                    {
                        sqlQuery = "({0} {1} {2})".Format(leftResult.SqlQuery, GetSqlName(binaryExpression), rightResult.SqlQuery);
                    }

                    return new CompilationResult { SqlQuery = sqlQuery };
                }
            }

            if (expression.NodeType == ExpressionType.Call)
            {
                var call = (MethodCallExpression)expression;
                var args = new CompilationResult[call.Arguments.Count];
                CompilationResult obj = call.Object != null ? CompileExpression(call.Object, queryParameters) : null;

                for (var i = 0; i < args.Length; i++)
                {
                    args[i] = CompileExpression(call.Arguments[i], queryParameters);
                }

                var sqlCall = "";

                if (call.Method.Name == "Like" && args.Length == 2)
                {
                    sqlCall = "(" + args[0].SqlQuery + " like " + args[1].SqlQuery + ")";
                }
                else if (call.Method.Name == "Contains" && args.Length == 2)
                {
                    sqlCall = "(" + args[1].SqlQuery + " in " + args[0].SqlQuery + ")";
                }
                else if (call.Method.Name == "Contains" && args.Length == 1)
                {
                    if (call.Object != null && call.Object.Type == typeof(string))
                    {
                        sqlCall = "(" + obj.SqlQuery + " like ('%' || " + args[0].SqlQuery + " || '%'))";
                    }
                    else
                    {
                        sqlCall = "(" + args[0].SqlQuery + " in " + obj.SqlQuery + ")";
                    }
                }
                else if (call.Method.Name == "StartsWith" && args.Length == 1)
                {
                    sqlCall = "(" + obj.SqlQuery + " like (" + args[0].SqlQuery + " || '%'))";
                }
                else if (call.Method.Name == "EndsWith" && args.Length == 1)
                {
                    sqlCall = "(" + obj.SqlQuery + " like ('%' || " + args[0].SqlQuery + "))";
                }
                else if (call.Method.Name == "Equals" && args.Length == 1)
                {
                    sqlCall = "(" + obj.SqlQuery + " = (" + args[0].SqlQuery + "))";
                }
                else if (call.Method.Name == "ToLower")
                {
                    sqlCall = "(lower(" + obj.SqlQuery + "))";
                }
                else if (call.Method.Name == "ToUpper")
                {
                    sqlCall = "(upper(" + obj.SqlQuery + "))";
                }
                else
                {
                    sqlCall = call.Method.Name.ToLower() + "(" +
                              string.Join(",", args.Select(a => a.SqlQuery).ToArray()) + ")";
                }

                return new CompilationResult { SqlQuery = sqlCall };
            }

            if (expression.NodeType == ExpressionType.Constant)
            {
                var c = (ConstantExpression)expression;
                queryParameters.Add(c.Value);
                return new CompilationResult { SqlQuery = "?", Value = c.Value };
            }

            if (expression.NodeType == ExpressionType.Convert)
            {
                var u = (UnaryExpression)expression;
                Type ty = u.Type;
                CompilationResult result = CompileExpression(u.Operand, queryParameters);
                return new CompilationResult { SqlQuery = result.SqlQuery, Value = result.Value != null ? ConvertTo(result.Value, ty) : null };
            }

            if (expression.NodeType == ExpressionType.Not)
            {
                var unaryExpression = (UnaryExpression)expression;
                CompilationResult result = CompileExpression(unaryExpression.Operand, queryParameters);

                return new CompilationResult { SqlQuery = "NOT " + result.SqlQuery, Value = result.Value };
            }

            if (expression.NodeType == ExpressionType.MemberAccess)
            {
                var memberExpression = (MemberExpression)expression;

                if (memberExpression.Expression is { NodeType: ExpressionType.Parameter })
                {
                    // This is a column of our table, output just the column name
                    // Need to translate it if that column name is mapped
                    var column = TableMapping.FindColumnWithPropertyName(memberExpression.Member.Name);
                    return new CompilationResult { SqlQuery = $"\"{column.Name}\"" };
                }

                object compilationResultValue = null;
                if (memberExpression.Expression != null)
                {
                    CompilationResult result = CompileExpression(memberExpression.Expression, queryParameters);
                    if (result.Value == null)
                    {
                        throw new NotSupportedException("Member access failed to compile expression");
                    }

                    if (result.SqlQuery == "?")
                    {
                        queryParameters.RemoveAt(queryParameters.Count - 1);
                    }

                    compilationResultValue = result.Value;
                }

                // Get the member value
                object value;

                if (memberExpression.Member.MemberType == MemberTypes.Property)
                {
                    var propertyInfo = (PropertyInfo)memberExpression.Member;
                    value = propertyInfo.GetGetMethod().Invoke(compilationResultValue, null);
                }
                else if (memberExpression.Member.MemberType == MemberTypes.Field)
                {
                    var fieldInfo = (FieldInfo)memberExpression.Member;
                    value = fieldInfo.GetValue(compilationResultValue);
                }
                else
                {
                    throw new NotSupportedException("MemberExpr: " + memberExpression.Member.MemberType);
                }

                // Work special magic for enumerables
                if (value is IEnumerable enumerable and not string and not IEnumerable<byte>)
                {
                    var stringBuilder = new StringBuilder();
                    stringBuilder.Append("(");
                    var head = "";
                    foreach (var element in enumerable)
                    {
                        queryParameters.Add(element);
                        stringBuilder.Append(head);
                        stringBuilder.Append("?");
                        head = ",";
                    }

                    stringBuilder.Append(")");
                    return new CompilationResult { SqlQuery = stringBuilder.ToString(), Value = enumerable };
                }

                queryParameters.Add(value);
                return new CompilationResult { SqlQuery = "?", Value = value };
            }

            throw new NotSupportedException("Cannot compile: " + expression.NodeType);
        }

        private static object ConvertTo(object obj, Type type)
        {
            Type underlyingType = Nullable.GetUnderlyingType(type);

            if (underlyingType != null)
            {
                return obj == null ? null : Convert.ChangeType(obj, underlyingType);
            }

            return Convert.ChangeType(obj, type);
        }

        /// <summary>
        ///     Compiles a BinaryExpression where one of the parameters is null.
        /// </summary>
        /// <param name="parameter">The non-null parameter</param>
        private string CompileNullBinaryExpression(BinaryExpression expression, CompilationResult parameter)
        {
            if (expression.NodeType == ExpressionType.Equal)
            {
                return "(" + parameter.SqlQuery + " is ?)";
            }

            if (expression.NodeType == ExpressionType.NotEqual)
            {
                return "(" + parameter.SqlQuery + " is not ?)";
            }

            throw new NotSupportedException(
                "Cannot compile Null-BinaryExpression with type " +
                expression.NodeType);
        }

        private string GetSqlName(Expression expression)
        {
            ExpressionType n = expression.NodeType;
            return n switch
            {
                ExpressionType.GreaterThan => ">",
                ExpressionType.GreaterThanOrEqual => ">=",
                ExpressionType.LessThan => "<",
                ExpressionType.LessThanOrEqual => "<=",
                ExpressionType.And => "&",
                ExpressionType.AndAlso => "and",
                ExpressionType.Or => "|",
                ExpressionType.OrElse => "or",
                ExpressionType.Equal => "=",
                ExpressionType.NotEqual => "!=",
                _ => throw new NotSupportedException("Cannot get SQL for: " + n)
            };
        }

        [PublicAPI]
        public int Count()
        {
            return BuildCommand("count(*)").ExecuteScalar<int>();
        }

        [PublicAPI]
        public int Count(Expression<Func<T, bool>> predicateExpression)
        {
            return Where(predicateExpression).Count();
        }

        [PublicAPI]
        public T First()
        {
            var query = Take(1);
            return query.ToList().First();
        }

        [PublicAPI]
        public T FirstOrDefault()
        {
            var query = Take(1);
            return query.ToList().FirstOrDefault();
        }

        private class CompilationResult
        {
            public string SqlQuery { get; set; }
            public object Value { get; set; }
        }
    }
}