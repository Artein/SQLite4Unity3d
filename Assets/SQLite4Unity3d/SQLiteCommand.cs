using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace SQLite4Unity3d
{
    [PublicAPI]
    public class SQLiteCommand
    {
        internal static readonly IntPtr NegativePointer = new(-1);
        private readonly List<Binding> _bindings = new();
        private readonly SQLiteConnection _connection;

        internal SQLiteCommand([NotNull] SQLiteConnection connection, string sqlQuery = "")
        {
            _connection = connection;
            SqlQuery = sqlQuery;
        }

        [PublicAPI] public string SqlQuery { get; set; }

        /// <returns>Affected rows count</returns>
        /// <exception cref="SQLiteException" />
        [PublicAPI]
        public int ExecuteNonQuery()
        {
            if (_connection.Trace)
            {
                _connection.InvokeTrace("Executing: " + this);
            }

            SQLite3.EResult result;
            lock (_connection.SyncObject)
            {
                using SQLite3.StatementHandle statementHandle = PrepareV2();
                result = SQLite3.Step(statementHandle);
            }

            if (result != SQLite3.EResult.Done)
            {
                throw SQLiteExceptionFactory.New("Could not execute non-query", result, _connection.ConnectionHandle);
            }

            var rowsAffected = SQLite3.Changes(_connection.ConnectionHandle);
            return rowsAffected;
        }

        [PublicAPI]
        public IEnumerable<T> ExecuteDeferredQuery<T>()
        {
            TableMapping mapping = _connection.GetMapping<T>();
            return ExecuteDeferredQuery<T>(mapping);
        }

        [PublicAPI]
        public List<T> ExecuteQuery<T>()
        {
            TableMapping mapping = _connection.GetMapping<T>();
            return ExecuteDeferredQuery<T>(mapping).ToList();
        }

        [PublicAPI]
        public List<T> ExecuteQuery<T>(TableMapping map)
        {
            return ExecuteDeferredQuery<T>(map).ToList();
        }

        /// <summary>
        ///     Invoked every time an instance is loaded from the database.
        /// </summary>
        /// <param name='instance'>The newly created object.</param>
        /// <remarks>
        ///     This can be overridden in combination with the <see cref="SQLiteConnection.NewCommand" />
        ///     method to hook into the life-cycle of objects.
        ///     Type safety is not possible because MonoTouch does not support virtual generic methods.
        /// </remarks>
        protected virtual void OnInstanceCreated(object instance)
        {
            // Can be overridden.
        }

        [PublicAPI]
        public IEnumerable<T> ExecuteDeferredQuery<T>(TableMapping mapping)
        {
            if (_connection.Trace)
            {
                _connection.InvokeTrace("Executing Query: " + this);
            }

            lock (_connection.SyncObject)
            {
                using SQLite3.StatementHandle statementHandle = PrepareV2();
                var columns = new TableMapping.Column[SQLite3.ColumnCount(statementHandle)];

                for (var i = 0; i < columns.Length; i += 1)
                {
                    CharPtr name = SQLite3.ColumnName16(statementHandle, i);
                    columns[i] = mapping.FindColumn(name);
                }

                while (SQLite3.Step(statementHandle) == SQLite3.EResult.Row)
                {
                    var instance = Activator.CreateInstance(mapping.MappedType);
                    for (var i = 0; i < columns.Length; i += 1)
                    {
                        if (columns[i] == null)
                        {
                            continue;
                        }

                        SQLite3.EColumnType columnType = SQLite3.ColumnType(statementHandle, i);
                        var columnValue = ReadColumn(statementHandle, i, columnType, columns[i].ColumnType);
                        columns[i].SetValue(instance, columnValue);
                    }

                    OnInstanceCreated(instance);
                    yield return (T)instance;
                }
            }
        }

        [PublicAPI]
        public T ExecuteScalar<T>()
        {
            if (_connection.Trace)
            {
                _connection.InvokeTrace("Executing Query: " + this);
            }

            var value = default(T);

            lock (_connection.SyncObject)
            {
                using SQLite3.StatementHandle statementHandle = PrepareV2();

                SQLite3.EResult result = SQLite3.Step(statementHandle);
                switch (result)
                {
                    case SQLite3.EResult.Done:
                        return value;
                    case SQLite3.EResult.Row:
                    {
                        SQLite3.EColumnType columnType = SQLite3.ColumnType(statementHandle, 0);
                        value = ReadColumn<T>(statementHandle, 0, columnType);
                        break;
                    }
                    default:
                        throw SQLiteExceptionFactory.New(
                            $"Couldn't execute {typeof(T).Name} scalar",
                            result,
                            _connection.ConnectionHandle);
                }
            }

            return value;
        }

        [PublicAPI]
        public void Bind(string name, object value)
        {
            _bindings.Add(new Binding { Name = name, Value = value });
        }

        [PublicAPI]
        public void Bind(object value)
        {
            Bind(null, value);
        }

        [PublicAPI]
        public override string ToString()
        {
            var parts = new string[1 + _bindings.Count];
            parts[0] = SqlQuery;
            var i = 1;
            foreach (Binding b in _bindings)
            {
                parts[i] = $"  {i - 1}: {b.Value}";
                i++;
            }

            return string.Join(Environment.NewLine, parts);
        }

        private SQLite3.StatementHandle PrepareV2()
        {
            SQLite3.StatementHandle statementHandle = SQLite3.PrepareV2(_connection.ConnectionHandle, SqlQuery);
            BindAll(statementHandle);
            return statementHandle;
        }

        private void BindAll(SQLite3.StatementHandle statementHandle)
        {
            var nextIdx = 1;
            foreach (Binding binding in _bindings)
            {
                binding.Index = binding.Name != null
                    ? SQLite3.BindParameterIndex(statementHandle, binding.Name)
                    : nextIdx++;
                BindParameter(statementHandle, binding.Index, binding.Value, _connection.StoreDateTimeAsTicks);
            }
        }

        internal static void BindParameter(SQLite3.StatementHandle statementHandle,
            int index,
            object value,
            bool storeDateTimeAsTicks)
        {
            switch (value)
            {
                case null:
                    SQLite3.BindNull(statementHandle, index);
                    break;
                case int intValue:
                    SQLite3.BindInt(statementHandle, index, intValue);
                    break;
                case string stringValue:
                    SQLite3.BindText16(statementHandle, index, stringValue, -1, NegativePointer);
                    break;
                case byte or ushort or sbyte or short:
                    SQLite3.BindInt(statementHandle, index, Convert.ToInt32(value));
                    break;
                case bool boolValue:
                    SQLite3.BindInt(statementHandle, index, boolValue ? 1 : 0);
                    break;
                case uint:
                case long:
                    SQLite3.BindInt64(statementHandle, index, Convert.ToInt64(value));
                    break;
                case float:
                case double:
                case decimal:
                    SQLite3.BindDouble(statementHandle, index, Convert.ToDouble(value));
                    break;
                case TimeSpan timeSpan:
                    SQLite3.BindInt64(statementHandle, index, timeSpan.Ticks);
                    break;
                case DateTime dateTime when storeDateTimeAsTicks:
                    SQLite3.BindInt64(statementHandle, index, dateTime.Ticks);
                    break;
                case DateTime dateTime:
                    SQLite3.BindText16(
                        statementHandle,
                        index,
                        dateTime.ToString("yyyy-MM-dd HH:mm:ss"),
                        -1,
                        NegativePointer);
                    break;
                case DateTimeOffset dateTimeOffset:
                    SQLite3.BindInt64(statementHandle, index, dateTimeOffset.UtcTicks);
                    break;
                default:
                {
                    if (value.GetType().IsEnum)
                    {
                        SQLite3.BindInt(statementHandle, index, Convert.ToInt32(value));
                    }
                    else
                    {
                        switch (value)
                        {
                            case byte[] bytes:
                                SQLite3.BindBlob(statementHandle, index, bytes, bytes.Length, NegativePointer);
                                break;
                            case Guid guid:
                                SQLite3.BindText16(statementHandle, index, guid.ToString(), 72, NegativePointer);
                                break;
                            default:
                                throw new NotSupportedException("Cannot store type: " + value.GetType());
                        }
                    }

                    break;
                }
            }
        }

        private T ReadColumn<T>(SQLite3.StatementHandle statementHandle, int index, SQLite3.EColumnType columnType)
        {
            return (T)ReadColumn(statementHandle, index, columnType, typeof(T));
        }

        private object ReadColumn(SQLite3.StatementHandle statementHandle,
            int index,
            SQLite3.EColumnType columnType,
            Type type)
        {
            if (columnType == SQLite3.EColumnType.Null)
            {
                return null;
            }

            if (type == typeof(string))
            {
                return SQLite3.ColumnText16(statementHandle, index);
            }

            if (type == typeof(int))
            {
                return SQLite3.ColumnInt(statementHandle, index);
            }

            if (type == typeof(bool))
            {
                return SQLite3.ColumnInt(statementHandle, index) == 1;
            }

            if (type == typeof(double))
            {
                return SQLite3.ColumnDouble(statementHandle, index);
            }

            if (type == typeof(float))
            {
                return (float)SQLite3.ColumnDouble(statementHandle, index);
            }

            if (type == typeof(TimeSpan))
            {
                return new TimeSpan(SQLite3.ColumnInt64(statementHandle, index));
            }

            if (type == typeof(DateTime))
            {
                if (_connection.StoreDateTimeAsTicks)
                {
                    return new DateTime(SQLite3.ColumnInt64(statementHandle, index));
                }

                CharPtr text = SQLite3.ColumnText16(statementHandle, index);
                return DateTime.Parse(text);
            }

            if (type == typeof(DateTimeOffset))
            {
                return new DateTimeOffset(SQLite3.ColumnInt64(statementHandle, index), TimeSpan.Zero);
            }

            if (type.IsEnum)
            {
                return SQLite3.ColumnInt(statementHandle, index);
            }

            if (type == typeof(long))
            {
                return SQLite3.ColumnInt64(statementHandle, index);
            }

            if (type == typeof(uint))
            {
                return (uint)SQLite3.ColumnInt64(statementHandle, index);
            }

            if (type == typeof(decimal))
            {
                return (decimal)SQLite3.ColumnDouble(statementHandle, index);
            }

            if (type == typeof(byte))
            {
                return (byte)SQLite3.ColumnInt(statementHandle, index);
            }

            if (type == typeof(ushort))
            {
                return (ushort)SQLite3.ColumnInt(statementHandle, index);
            }

            if (type == typeof(short))
            {
                return (short)SQLite3.ColumnInt(statementHandle, index);
            }

            if (type == typeof(sbyte))
            {
                return (sbyte)SQLite3.ColumnInt(statementHandle, index);
            }

            if (type == typeof(byte[]))
            {
                return SQLite3.ColumnByteArray(statementHandle, index);
            }

            if (type == typeof(Guid))
            {
                CharPtr text = SQLite3.ColumnText16(statementHandle, index);
                return new Guid(text);
            }

            throw new NotSupportedException("Don't know how to read " + type);
        }

        private class Binding
        {
            public string Name { get; set; }
            public object Value { get; set; }
            public int Index { get; set; }
        }
    }
}