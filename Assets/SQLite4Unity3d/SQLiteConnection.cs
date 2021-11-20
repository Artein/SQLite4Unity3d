using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using JetBrains.Annotations;
using SQLite4Unity3d.Attributes;
using SQLite4Unity3d.Utils;
using UnityEngine.Assertions;
using UnityEngine.Scripting;

namespace SQLite4Unity3d
{
    /// <summary>
    ///     Represents an open connection to a SQLite database.
    /// </summary>
    [PublicAPI]
    public class SQLiteConnection : IDisposable
    {
        // Dictionary of synchronization objects.
        //
        // To prevent database disruption, a database file must be accessed *synchronously*.
        // For the purpose we create synchronous objects for each database file and store in the
        // static dictionary to share it among all connections.
        // The key of the dictionary is database file path and its value is an object to be used
        // by lock() statement.
        //
        // Use case:
        // - database file lock is done implicitly and automatically.
        // - To prepend deadlock, application may lock a database file explicitly by either way:
        //   - RunInTransaction(Action) locks the database during the transaction (for insert/update)
        //   - RunInDatabaseLock(Action) similarly locks the database but no transaction (for query)
        private static Dictionary<string, object> _syncObjects = new();

        private TimeSpan _busyTimeout;
        private TimeSpan _elapsed;
        private bool _isOpen;
        private Dictionary<string, TableMapping> _mappings;
        private Random _rand = new();
        private Stopwatch _sw;
        private Dictionary<string, TableMapping> _tables;

        private int _transactionDepth;

        /// <summary>
        ///     Constructs a new SQLiteConnection and opens a SQLite database specified by databasePath.
        /// </summary>
        /// <param name="databasePath">
        ///     Specifies the path to the database file.
        /// </param>
        /// <param name="storeDateTimeAsTicks">
        ///     Specifies whether to store DateTime properties as ticks (true) or strings (false). You
        ///     absolutely do want to store them as Ticks in all new projects. The default of false is
        ///     only here for backwards compatibility. There is a *significant* speed advantage, with no
        ///     down sides, when setting storeDateTimeAsTicks = true.
        /// </param>
        [PublicAPI]
        public SQLiteConnection(string databasePath, bool storeDateTimeAsTicks = false)
            : this(databasePath, SQLite3.EOpenFlags.ReadWrite | SQLite3.EOpenFlags.Create, storeDateTimeAsTicks)
        {
        }

        /// <summary>
        ///     Constructs a new SQLiteConnection and opens a SQLite database specified by databasePath.
        /// </summary>
        /// <param name="databasePath">
        ///     Specifies the path to the database file.
        /// </param>
        /// <param name="openFlags"></param>
        /// <param name="storeDateTimeAsTicks">
        ///     Specifies whether to store DateTime properties as ticks (true) or strings (false). You
        ///     absolutely do want to store them as Ticks in all new projects. The default of false is
        ///     only here for backwards compatibility. There is a *significant* speed advantage, with no
        ///     down sides, when setting storeDateTimeAsTicks = true.
        /// </param>
        [PublicAPI]
        public SQLiteConnection(string databasePath, SQLite3.EOpenFlags openFlags, bool storeDateTimeAsTicks = false)
        {
            if (string.IsNullOrEmpty(databasePath))
            {
                throw new ArgumentException("Must be specified", nameof(databasePath));
            }

            DatabasePath = databasePath;
            MayCreateSyncObject(databasePath);

            SQLite3.EResult result = SQLite3.OpenV2(DatabasePath, out SQLite3.DBConnectionV2Handle connectionV2Handle, (int)openFlags, null);
            if (result != SQLite3.EResult.OK)
            {
                throw SQLiteExceptionFactory.New($"Couldn't open database file: {DatabasePath} ({result})", result);
            }

            ConnectionHandle = connectionV2Handle;
            _isOpen = true;
            StoreDateTimeAsTicks = storeDateTimeAsTicks;
            BusyTimeout = TimeSpan.FromSeconds(0.1);
        }

        [PublicAPI] public SQLite3.DBConnectionHandle ConnectionHandle { get; private set; }
        [PublicAPI] public string DatabasePath { get; private set; }
        [PublicAPI] public bool StoreDateTimeAsTicks { get; private set; }

        /// <summary>
        ///     Gets the synchronous object, to be lock the database file for updating.
        /// </summary>
        /// <value>The sync object.</value>
        [PublicAPI]
        public object SyncObject => _syncObjects[DatabasePath];

        /// <summary>
        ///     Sets a busy handler to sleep the specified amount of time when a table is locked.
        ///     The handler will sleep multiple times until a total time of <see cref="BusyTimeout" /> has accumulated.
        /// </summary>
        [PublicAPI]
        public TimeSpan BusyTimeout
        {
            get => _busyTimeout;
            set
            {
                _busyTimeout = value;
                if (ConnectionHandle != null)
                {
                    SQLite3.BusyTimeout(ConnectionHandle, (int)_busyTimeout.TotalMilliseconds);
                }
            }
        }

        /// <summary>
        ///     Returns the mappings from types to tables that the connection
        ///     currently understands.
        /// </summary>
        [PublicAPI]
        public IEnumerable<TableMapping> TableMappings => _tables?.Values ?? Enumerable.Empty<TableMapping>();

        /// <summary>
        ///     Whether <see cref="BeginTransaction" /> has been called and the database is waiting for a <see cref="Commit" />.
        /// </summary>
        [PublicAPI]
        public bool IsInTransaction => _transactionDepth > 0;

        public void Dispose()
        {
            Dispose(EDisposeCaller.Disposing);
            GC.SuppressFinalize(this);
        }

        private void MayCreateSyncObject(string databasePath)
        {
            if (!_syncObjects.ContainsKey(databasePath))
            {
                _syncObjects[databasePath] = new object();
            }
        }

        [PublicAPI]
        public void EnableLoadExtension(int enable)
        {
            SQLite3.EResult result = SQLite3.EnableLoadExtension(ConnectionHandle, enable);
            if (result != SQLite3.EResult.OK)
            {
                throw SQLiteExceptionFactory.New(
                    $"Couldn't set load extension to '{enable}'",
                    result,
                    ConnectionHandle);
            }
        }

        /// <summary>
        ///     Retrieves the mapping that is automatically generated for the given type.
        /// </summary>
        /// <param name="createFlags">
        ///     Optional flags allowing implicit PK and indexes based on naming conventions
        /// </param>
        /// <returns>
        ///     The mapping represents the schema of the columns of the database and contains
        ///     methods to set and get properties of objects.
        /// </returns>
        [PublicAPI]
        public TableMapping GetMapping<T>(SQLite3.ECreateFlags createFlags = SQLite3.ECreateFlags.None)
        {
            return GetMapping(typeof(T), createFlags);
        }

        /// <see cref="GetMapping{T}" />
        [PublicAPI]
        public TableMapping GetMapping(Type type, SQLite3.ECreateFlags createFlags = SQLite3.ECreateFlags.None)
        {
            Assert.IsNotNull(type.FullName);
            _mappings ??= new Dictionary<string, TableMapping>();
            if (_mappings.TryGetValue(type.FullName, out TableMapping map))
            {
                return map;
            }

            map = new TableMapping(type, createFlags);
            _mappings[type.FullName] = map;

            return map;
        }

        /// <summary>
        ///     Executes a "drop table" on the database.  This is non-recoverable.
        /// </summary>
        [PublicAPI]
        public int DropTable<T>()
        {
            TableMapping map = GetMapping<T>();
            var sqlQuery = $"drop table if exists \"{map.TableName}\"";
            return Execute(sqlQuery);
        }

        /// <summary>
        ///     Executes a "create table if not exists" on the database. It also
        ///     creates any specified indexes on the columns of the table. It uses
        ///     a schema automatically generated from the specified type. You can
        ///     later access this schema by calling GetMapping.
        /// </summary>
        /// <param name="createFlags">Optional flags allowing implicit PK and indexes based on naming conventions.</param>
        /// <returns>
        ///     The number of entries added to the database schema.
        /// </returns>
        [PublicAPI]
        public int CreateTable<T>(SQLite3.ECreateFlags createFlags = SQLite3.ECreateFlags.None)
        {
            Type type = typeof(T);
            Assert.IsNotNull(type.FullName);
            _tables ??= new Dictionary<string, TableMapping>();
            if (!_tables.TryGetValue(type.FullName, out TableMapping map))
            {
                map = GetMapping<T>(createFlags);
                _tables.Add(type.FullName, map);
            }

            var decls = map.Columns.Select(p => Orm.SqlDecl(p, StoreDateTimeAsTicks));
            var decl = string.Join(",\n", decls.ToArray());
            var sqlQuery = $"create table if not exists \"{map.TableName}\"(\n{decl})";

            var count = Execute(sqlQuery);

            if (count == 0) //Possible bug: This always seems to return 0?
                // Table already exists, migrate it
            {
                MigrateTable(map);
            }

            var indexes = new Dictionary<string, IndexInfo>();
            foreach (TableMapping.Column column in map.Columns)
            {
                foreach (IndexedAttribute index in column.Indices)
                {
                    var indexName = index.Name ?? map.TableName + "_" + column.Name;
                    if (!indexes.TryGetValue(indexName, out IndexInfo indexInfo))
                    {
                        indexInfo = new IndexInfo
                        {
                            IndexName = indexName, TableName = map.TableName, Unique = index.Unique, Columns = new List<IndexedColumn>(),
                        };
                        indexes.Add(indexName, indexInfo);
                    }

                    if (index.Unique != indexInfo.Unique)
                    {
                        throw new Exception("All the columns in an index must have the same value for their Unique property");
                    }

                    indexInfo.Columns.Add(new IndexedColumn { Order = index.Order, ColumnName = column.Name });
                }
            }

            foreach (var indexName in indexes.Keys)
            {
                IndexInfo index = indexes[indexName];
                var columnNames = new string[index.Columns.Count];
                if (index.Columns.Count == 1)
                {
                    columnNames[0] = index.Columns[0].ColumnName;
                }
                else
                {
                    index.Columns.Sort((lhs, rhs) => lhs.Order - rhs.Order);
                    for (int i = 0, end = index.Columns.Count; i < end; ++i)
                    {
                        columnNames[i] = index.Columns[i].ColumnName;
                    }
                }

                count += CreateIndex(indexName, index.TableName, columnNames, index.Unique);
            }

            return count;
        }

        /// <summary>
        ///     Creates an index for the specified table and columns.
        /// </summary>
        /// <param name="indexName">Name of the index to create</param>
        /// <param name="tableName">Name of the database table</param>
        /// <param name="columnNames">An array of column names to index</param>
        /// <param name="isUnique">Whether the index should be unique</param>
        [PublicAPI]
        public int CreateIndex(string indexName,
            string tableName,
            string[] columnNames,
            bool isUnique = false)
        {
            var sqlQuery =
                $"create {(isUnique ? "unique" : "")} index " +
                $"if not exists \"{indexName}\" on \"{tableName}\"(\"{string.Join("\", \"", columnNames)}\")";
            return Execute(sqlQuery);
        }

        /// <summary>
        ///     Creates an index for the specified table and column.
        /// </summary>
        /// <param name="indexName">Name of the index to create</param>
        /// <param name="tableName">Name of the database table</param>
        /// <param name="columnName">Name of the column to index</param>
        /// <param name="isUnique">Whether the index should be unique</param>
        [PublicAPI]
        public int CreateIndex(string indexName,
            string tableName,
            string columnName,
            bool isUnique = false)
        {
            return CreateIndex(indexName, tableName, new[] { columnName }, isUnique);
        }

        /// <summary>
        ///     Creates an index for the specified table and column.
        /// </summary>
        /// <param name="tableName">Name of the database table</param>
        /// <param name="columnName">Name of the column to index</param>
        /// <param name="isUnique">Whether the index should be unique</param>
        [PublicAPI]
        public int CreateIndex(string tableName, string columnName, bool isUnique = false)
        {
            return CreateIndex($"{tableName}_{columnName}", tableName, columnName, isUnique);
        }

        /// <summary>
        ///     Creates an index for the specified table and columns.
        /// </summary>
        /// <param name="tableName">Name of the database table</param>
        /// <param name="columnNames">An array of column names to index</param>
        /// <param name="isUnique">Whether the index should be unique</param>
        [PublicAPI]
        public int CreateIndex(string tableName, string[] columnNames, bool isUnique = false)
        {
            return CreateIndex($"{tableName}_{string.Join("_", columnNames)}", tableName, columnNames, isUnique);
        }

        /// <summary>
        ///     Creates an index for the specified object property.
        ///     e.g. CreateIndex<Client>(c => c.Name);
        /// </summary>
        /// <typeparam name="T">Type to reflect to a database table.</typeparam>
        /// <param name="property">Property to index</param>
        /// <param name="isUnique">Whether the index should be unique</param>
        [PublicAPI]
        public void CreateIndex<T>(Expression<Func<T, object>> property, bool isUnique = false)
        {
            MemberExpression memberExpression = property.Body.NodeType == ExpressionType.Convert
                ? ((UnaryExpression)property.Body).Operand as MemberExpression
                : property.Body as MemberExpression;

            Assert.IsNotNull(memberExpression);
            var propertyInfo = memberExpression.Member as PropertyInfo;
            if (propertyInfo == null)
            {
                throw new ArgumentException("The lambda expression 'property' should point to a valid Property");
            }

            TableMapping mapping = GetMapping<T>();
            var columnName = mapping.FindColumnWithPropertyName(propertyInfo.Name).Name;

            CreateIndex(mapping.TableName, columnName, isUnique);
        }

        [PublicAPI]
        public List<ColumnInfo> GetTableInfo(string tableName)
        {
            var sqlQuery = $"pragma table_info(\"{tableName}\")";
            return Query<ColumnInfo>(sqlQuery);
        }

        private void MigrateTable(TableMapping mapping)
        {
            var existingColumns = GetTableInfo(mapping.TableName);
            var columnsToAdd = new List<TableMapping.Column>();

            foreach (TableMapping.Column column in mapping.Columns)
            {
                var found = existingColumns.Any(ec => column.Name.CompareTo(ec.Name, StringComparison.OrdinalIgnoreCase) == 0);
                if (!found)
                {
                    columnsToAdd.Add(column);
                }
            }

            var baseQuery = $"alter table \"{mapping.TableName}\" add column ";
            columnsToAdd.ForEach(column => Execute(baseQuery + Orm.SqlDecl(column, StoreDateTimeAsTicks)));
        }

        /// <summary>
        ///     Creates a new SQLiteCommand given the command text with arguments. Place a '?'
        ///     in the command text for each of the arguments.
        /// </summary>
        /// <param name="sqlQuery">The fully escaped SQL.</param>
        /// <param name="parameters">Parameters to substitute for the occurrences of '?' in the command text.</param>
        /// <returns>A <see cref="SQLiteCommand" /></returns>
        [PublicAPI]
        public SQLiteCommand CreateCommand(string sqlQuery, params object[] parameters)
        {
            if (!_isOpen)
            {
                throw SQLiteExceptionFactory.New("Cannot create commands from unopened database", SQLite3.EResult.Error);
            }

            var command = new SQLiteCommand(this, sqlQuery);
            parameters.ForEach(p => command.Bind(p));
            return command;
        }

        /// <summary>
        ///     Creates a SQLiteCommand given the command text (SQL) with arguments. Place a '?'
        ///     in the command text for each of the arguments and then executes that command.
        ///     Use this method instead of Query when you don't expect rows back. Such cases include
        ///     INSERTs, UPDATEs, and DELETEs.
        ///     You can set the Trace or TimeExecution properties of the connection
        ///     to profile execution.
        /// </summary>
        /// <param name="sqlQuery">The fully escaped SQL.</param>
        /// <param name="parameters">Arguments to substitute for the occurrences of '?' in the query.</param>
        /// <returns>Affected rows count.</returns>
        [PublicAPI]
        public int Execute(string sqlQuery, params object[] parameters)
        {
            SQLiteCommand command = CreateCommand(sqlQuery, parameters);

            if (TimeExecution)
            {
                _sw ??= new Stopwatch();
                _sw.Reset();
                _sw.Start();
            }

            var affectedRowsCount = command.ExecuteNonQuery();

            if (TimeExecution)
            {
                _sw.Stop();
                _elapsed += _sw.Elapsed;
                InvokeTimeExecution(_sw.Elapsed, _elapsed);
            }

            return affectedRowsCount;
        }

        [PublicAPI]
        public T ExecuteScalar<T>(string sqlQuery, params object[] parameters)
        {
            SQLiteCommand cmd = CreateCommand(sqlQuery, parameters);

            if (TimeExecution)
            {
                _sw ??= new Stopwatch();
                _sw.Reset();
                _sw.Start();
            }

            var result = cmd.ExecuteScalar<T>();

            if (TimeExecution)
            {
                _sw.Stop();
                _elapsed += _sw.Elapsed;
                InvokeTimeExecution(_sw.Elapsed, _elapsed);
            }

            return result;
        }

        /// <summary>
        ///     Creates a SQLiteCommand given the command text (SQL) with arguments. Place a '?'
        ///     in the command text for each of the arguments and then executes that command.
        ///     It returns each row of the result using the mapping automatically generated for
        ///     the given type.
        /// </summary>
        /// <param name="sqlQuery">The fully escaped SQL.</param>
        /// <param name="parameters">Arguments to substitute for the occurrences of '?' in the query.</param>
        /// <returns>An enumerable with one result for each row returned by the query.</returns>
        [PublicAPI]
        public List<T> Query<T>(string sqlQuery, params object[] parameters) where T : new()
        {
            SQLiteCommand command = CreateCommand(sqlQuery, parameters);
            return command.ExecuteQuery<T>();
        }

        /// <summary>
        ///     Creates a SQLiteCommand given the command text (SQL) with arguments. Place a '?'
        ///     in the command text for each of the arguments and then executes that command.
        ///     It returns each row of the result using the mapping automatically generated for
        ///     the given type.
        /// </summary>
        /// <param name="sqlQuery">The fully escaped SQL.</param>
        /// <param name="parameters">Arguments to substitute for the occurrences of '?' in the query.</param>
        /// <returns>
        ///     An enumerable with one result for each row returned by the query.
        ///     The enumerator will call sqlite3_step on each call to MoveNext, so the database
        ///     connection must remain open for the lifetime of the enumerator.
        /// </returns>
        [PublicAPI]
        public IEnumerable<T> DeferredQuery<T>(string sqlQuery, params object[] parameters) where T : new()
        {
            SQLiteCommand command = CreateCommand(sqlQuery, parameters);
            return command.ExecuteDeferredQuery<T>();
        }

        /// <summary>
        ///     Creates a SQLiteCommand given the command text (SQL) with arguments. Place a '?'
        ///     in the command text for each of the arguments and then executes that command.
        ///     It returns each row of the result using the specified mapping. This function is
        ///     only used by libraries in order to query the database via introspection. It is
        ///     normally not used.
        /// </summary>
        /// <param name="mapping">A <see cref="TableMapping" /> to use to convert the resulting rows into objects.</param>
        /// <param name="sqlQuery">The fully escaped SQL.</param>
        /// <param name="parameters">Arguments to substitute for the occurrences of '?' in the query.</param>
        /// <returns>An enumerable with one result for each row returned by the query.</returns>
        [PublicAPI]
        public List<object> Query(TableMapping mapping, string sqlQuery, params object[] parameters)
        {
            SQLiteCommand command = CreateCommand(sqlQuery, parameters);
            return command.ExecuteQuery<object>(mapping);
        }

        /// <summary>
        ///     Creates a SQLiteCommand given the command text (SQL) with arguments. Place a '?'
        ///     in the command text for each of the arguments and then executes that command.
        ///     It returns each row of the result using the specified mapping. This function is
        ///     only used by libraries in order to query the database via introspection. It is
        ///     normally not used.
        /// </summary>
        /// <param name="mapping">A <see cref="TableMapping" /> to use to convert the resulting rows into objects.</param>
        /// <param name="sqlQuery">The fully escaped SQL.</param>
        /// <param name="parameters">Arguments to substitute for the occurrences of '?' in the query.</param>
        /// <returns>
        ///     An enumerable with one result for each row returned by the query.
        ///     The enumerator will call sqlite3_step on each call to MoveNext, so the database
        ///     connection must remain open for the lifetime of the enumerator.
        /// </returns>
        [PublicAPI]
        public IEnumerable<object> DeferredQuery(TableMapping mapping, string sqlQuery, params object[] parameters)
        {
            SQLiteCommand command = CreateCommand(sqlQuery, parameters);
            return command.ExecuteDeferredQuery<object>(mapping);
        }

        /// <summary>
        ///     Returns a queryable interface to the table represented by the given type.
        /// </summary>
        /// <returns>
        ///     A queryable object that is able to translate Where, OrderBy, and Take
        ///     queries into native SQL.
        /// </returns>
        [PublicAPI]
        public TableQuery<T> Table<T>() where T : new()
        {
            return new TableQuery<T>(this);
        }

        /// <summary>
        ///     Attempts to retrieve an object with the given primary key from the table
        ///     associated with the specified type. Use of this method requires that
        ///     the given type have a designated PrimaryKey (using the PrimaryKeyAttribute).
        /// </summary>
        /// <param name="primaryKeyObject">
        ///     The primary key.
        /// </param>
        /// <returns>
        ///     The object with the given primary key. Throws a not found exception
        ///     if the object is not found.
        /// </returns>
        [PublicAPI]
        public T Get<T>(object primaryKeyObject) where T : new()
        {
            TableMapping map = GetMapping<T>();
            return Query<T>(map.GetByPrimaryKeySql, primaryKeyObject).First();
        }

        /// <summary>
        ///     Attempts to retrieve the first object that matches the predicate from the table
        ///     associated with the specified type.
        /// </summary>
        /// <param name="predicate">
        ///     A predicate for which object to find.
        /// </param>
        /// <returns>
        ///     The object that matches the given predicate. Throws a not found exception
        ///     if the object is not found.
        /// </returns>
        [PublicAPI]
        public T Get<T>(Expression<Func<T, bool>> predicate) where T : new()
        {
            return Table<T>().Where(predicate).First();
        }

        /// <summary>
        ///     Attempts to retrieve an object with the given primary key from the table
        ///     associated with the specified type. Use of this method requires that
        ///     the given type have a designated PrimaryKey (using the PrimaryKeyAttribute).
        /// </summary>
        /// <param name="primaryKeyObject">
        ///     The primary key.
        /// </param>
        /// <returns>
        ///     The object with the given primary key or null
        ///     if the object is not found.
        /// </returns>
        [PublicAPI]
        public T Find<T>(object primaryKeyObject) where T : new()
        {
            TableMapping map = GetMapping<T>();
            return Query<T>(map.GetByPrimaryKeySql, primaryKeyObject).FirstOrDefault();
        }

        /// <summary>
        ///     Attempts to retrieve an object with the given primary key from the table
        ///     associated with the specified type. Use of this method requires that
        ///     the given type have a designated PrimaryKey (using the PrimaryKeyAttribute).
        /// </summary>
        /// <param name="primaryKeyObject">
        ///     The primary key.
        /// </param>
        /// <param name="map">
        ///     The TableMapping used to identify the object type.
        /// </param>
        /// <returns>
        ///     The object with the given primary key or null
        ///     if the object is not found.
        /// </returns>
        [PublicAPI]
        public object Find(object primaryKeyObject, TableMapping map)
        {
            return Query(map, map.GetByPrimaryKeySql, primaryKeyObject).FirstOrDefault();
        }

        /// <summary>
        ///     Attempts to retrieve the first object that matches the predicate from the table
        ///     associated with the specified type.
        /// </summary>
        /// <param name="predicate">
        ///     A predicate for which object to find.
        /// </param>
        /// <returns>
        ///     The object that matches the given predicate or null
        ///     if the object is not found.
        /// </returns>
        [PublicAPI]
        public T Find<T>(Expression<Func<T, bool>> predicate) where T : new()
        {
            return Table<T>().Where(predicate).FirstOrDefault();
        }

        /// <summary>
        ///     Begins a new transaction. Call <see cref="Commit" /> to end the transaction.
        /// </summary>
        /// <example cref="System.InvalidOperationException">Throws if a transaction has already begun.</example>
        public void BeginTransaction()
        {
            // The BEGIN command only works if the transaction stack is empty, or in other words if there are no pending transactions.
            // If the transaction stack is not empty when the BEGIN command is invoked, then the command fails with an error.
            // Rather than crash with an error, we will just ignore calls to BeginTransaction that would result in an error.
            if (Interlocked.CompareExchange(ref _transactionDepth, 1, 0) != 0)
            {
                // Calling BeginTransaction on an already open transaction is invalid
                throw new InvalidOperationException("Cannot begin a transaction while already in a transaction.");
            }

            try
            {
                Execute("begin transaction");
            }
            catch (Exception ex)
            {
                if (ex is SQLiteException sqLiteException)
                {
                    // TODO: This rollback failsafe should be localized to all throw sites.
                    switch (sqLiteException.Result)
                    {
                        case SQLite3.EResult.IOError:
                        case SQLite3.EResult.Full:
                        case SQLite3.EResult.Busy:
                        case SQLite3.EResult.NoMem:
                        case SQLite3.EResult.Interrupt:
                            Rollback(null, false);
                            break;
                    }
                }
                else // Call decrement and not VolatileWrite in case we've already created a transaction point in SaveTransactionPoint since the catch
                {
                    Interlocked.Decrement(ref _transactionDepth);
                }

                throw;
            }
        }

        /// <summary>
        ///     Creates a savepoint in the database at the current point in the transaction timeline.
        ///     Begins a new transaction if one is not in progress.
        ///     Call <see cref="Rollback" /> to undo transactions since the returned savepoint.
        ///     Call <see cref="Release" /> to commit transactions after the savepoint returned here.
        ///     Call <see cref="Commit" /> to end the transaction, committing all changes.
        /// </summary>
        /// <returns>A string naming the savepoint.</returns>
        public string SaveTransactionPoint()
        {
            var depth = Interlocked.Increment(ref _transactionDepth) - 1;
            var retVal = "S" + _rand.Next(short.MaxValue) + "D" + depth;

            try
            {
                Execute("savepoint " + retVal);
            }
            catch (Exception ex)
            {
                var sqlExp = ex as SQLiteException;
                if (sqlExp != null) // It is recommended that applications respond to the errors listed below
                    //    by explicitly issuing a ROLLBACK command.
                    // TODO: This rollback failsafe should be localized to all throw sites.
                {
                    switch (sqlExp.Result)
                    {
                        case SQLite3.EResult.IOError:
                        case SQLite3.EResult.Full:
                        case SQLite3.EResult.Busy:
                        case SQLite3.EResult.NoMem:
                        case SQLite3.EResult.Interrupt:
                            Rollback(null, false);
                            break;
                    }
                }
                else
                {
                    Interlocked.Decrement(ref _transactionDepth);
                }

                throw;
            }

            return retVal;
        }

        /// <summary>
        ///     Rolls back the transaction that was begun by <see cref="BeginTransaction" />.
        /// </summary>
        /// <param name="savepointName">Name of the savepoint</param>
        /// <param name="throwExceptionIfAny">Rethrow an exception if any occured</param>
        private void Rollback(string savepointName = null, bool throwExceptionIfAny = true)
        {
            // Rolling back without a TO clause rolls backs all transactions and leaves the transaction stack empty.
            try
            {
                if (string.IsNullOrEmpty(savepointName))
                {
                    if (Interlocked.Exchange(ref _transactionDepth, 0) > 0)
                    {
                        Execute("rollback");
                    }
                }
                else
                {
                    DoSavePointExecute(savepointName, "rollback to ");
                }
            }
            catch (SQLiteException)
            {
                if (throwExceptionIfAny)
                {
                    throw;
                }
            }
            // No need to rollback if there are no transactions open.
        }

        /// <summary>
        ///     Releases a savepoint returned from <see cref="SaveTransactionPoint" />.  Releasing a savepoint
        ///     makes changes since that savepoint permanent if the savepoint began the transaction,
        ///     or otherwise the changes are permanent pending a call to <see cref="Commit" />.
        ///     The RELEASE command is like a COMMIT for a SAVEPOINT.
        /// </summary>
        /// <param name="savepoint">
        ///     The name of the savepoint to release.  The string should be the result of a call to
        ///     <see cref="SaveTransactionPoint" />
        /// </param>
        public void Release(string savepoint)
        {
            DoSavePointExecute(savepoint, "release ");
        }

        private void DoSavePointExecute(string savepoint, string cmd)
        {
            // Validate the savepoint
            var firstLen = savepoint.IndexOf('D');
            if (firstLen >= 2 && savepoint.Length > firstLen + 1)
            {
                int depth;
                if (int.TryParse(
                    savepoint.Substring(firstLen + 1),
                    out depth)) // TODO: Mild race here, but inescapable without locking almost everywhere.
                {
                    if (0 <= depth && depth < _transactionDepth)
                    {
                        Thread.VolatileWrite(ref _transactionDepth, depth);
                        Execute(cmd + savepoint);
                        return;
                    }
                }
            }

            throw new ArgumentException(
                "savePoint is not valid, and should be the result of a call to SaveTransactionPoint.",
                "savePoint");
        }

        /// <summary>
        ///     Commits the transaction that was begun by <see cref="BeginTransaction" />.
        /// </summary>
        public void Commit()
        {
            if (Interlocked.Exchange(ref _transactionDepth, 0) != 0)
            {
                Execute("commit");
            }
            // Do nothing on a commit with no open transaction
        }

        /// <summary>
        ///     Executes
        ///     <param name="action">
        ///         within a (possibly nested) transaction by wrapping it in a SAVEPOINT. If an
        ///         exception occurs the whole transaction is rolled back, not just the current savepoint. The exception
        ///         is rethrown.
        /// </summary>
        /// <param name="action">
        ///     The <see cref="Action" /> to perform within a transaction.
        ///     <param name="action">
        ///         can contain any number
        ///         of operations on the connection but should never call <see cref="BeginTransaction" /> or
        ///         <see cref="Commit" />.
        ///     </param>
        public void RunInTransaction(Action action)
        {
            try
            {
                lock (_syncObjects[DatabasePath])
                {
                    var savePoint = SaveTransactionPoint();
                    action();
                    Release(savePoint);
                }
            }
            catch (Exception)
            {
                Rollback();
                throw;
            }
        }

        /// <summary>
        ///     Executes
        ///     <param name="action"> while blocking other threads to access the same database.
        /// </summary>
        /// <param name="action">
        ///     The <see cref="Action" /> to perform within a lock.
        /// </param>
        public void RunInDatabaseLock(Action action)
        {
            lock (_syncObjects[DatabasePath])
            {
                action();
            }
        }

        /// <summary>
        ///     Inserts all specified objects.
        /// </summary>
        /// <param name="objects">
        ///     An <see cref="IEnumerable" /> of the objects to insert.
        /// </param>
        /// <returns>
        ///     The number of rows added to the table.
        /// </returns>
        public int InsertAll(IEnumerable objects)
        {
            var c = 0;
            RunInTransaction(
                () =>
                {
                    foreach (var r in objects)
                    {
                        c += Insert(r);
                    }
                });
            return c;
        }

        /// <summary>
        ///     Inserts all specified objects.
        /// </summary>
        /// <param name="objects">
        ///     An <see cref="IEnumerable" /> of the objects to insert.
        /// </param>
        /// <param name="extra">
        ///     Literal SQL code that gets placed into the command. INSERT {extra} INTO ...
        /// </param>
        /// <returns>
        ///     The number of rows added to the table.
        /// </returns>
        public int InsertAll(IEnumerable objects, string extra)
        {
            var c = 0;
            RunInTransaction(
                () =>
                {
                    foreach (var r in objects)
                    {
                        c += Insert(r, extra);
                    }
                });
            return c;
        }

        /// <summary>
        ///     Inserts all specified objects.
        /// </summary>
        /// <param name="objects">
        ///     An <see cref="IEnumerable" /> of the objects to insert.
        /// </param>
        /// <param name="objType">
        ///     The type of object to insert.
        /// </param>
        /// <returns>
        ///     The number of rows added to the table.
        /// </returns>
        public int InsertAll(IEnumerable objects, Type objType)
        {
            var c = 0;
            RunInTransaction(
                () =>
                {
                    foreach (var r in objects)
                    {
                        c += Insert(r, objType);
                    }
                });
            return c;
        }

        /// <summary>
        ///     Inserts the given object and retrieves its
        ///     auto incremented primary key if it has one.
        /// </summary>
        /// <param name="obj">
        ///     The object to insert.
        /// </param>
        /// <returns>
        ///     The number of rows added to the table.
        /// </returns>
        public int Insert(object obj)
        {
            if (obj == null)
            {
                return 0;
            }

            return Insert(obj, "", obj.GetType());
        }

        /// <summary>
        ///     Inserts the given object and retrieves its
        ///     auto incremented primary key if it has one.
        ///     If a UNIQUE constraint violation occurs with
        ///     some pre-existing object, this function deletes
        ///     the old object.
        /// </summary>
        /// <param name="obj">
        ///     The object to insert.
        /// </param>
        /// <returns>
        ///     The number of rows modified.
        /// </returns>
        public int InsertOrReplace(object obj)
        {
            if (obj == null)
            {
                return 0;
            }

            return Insert(obj, "OR REPLACE", obj.GetType());
        }

        /// <summary>
        ///     Inserts the given object and retrieves its
        ///     auto incremented primary key if it has one.
        /// </summary>
        /// <param name="obj">
        ///     The object to insert.
        /// </param>
        /// <param name="objType">
        ///     The type of object to insert.
        /// </param>
        /// <returns>
        ///     The number of rows added to the table.
        /// </returns>
        public int Insert(object obj, Type objType)
        {
            return Insert(obj, "", objType);
        }

        /// <summary>
        ///     Inserts the given object and retrieves its
        ///     auto incremented primary key if it has one.
        ///     If a UNIQUE constraint violation occurs with
        ///     some pre-existing object, this function deletes
        ///     the old object.
        /// </summary>
        /// <param name="obj">
        ///     The object to insert.
        /// </param>
        /// <param name="objType">
        ///     The type of object to insert.
        /// </param>
        /// <returns>
        ///     The number of rows modified.
        /// </returns>
        public int InsertOrReplace(object obj, Type objType)
        {
            return Insert(obj, "OR REPLACE", objType);
        }

        /// <summary>
        ///     Inserts the given object and retrieves its
        ///     auto incremented primary key if it has one.
        /// </summary>
        /// <param name="obj">
        ///     The object to insert.
        /// </param>
        /// <param name="extra">
        ///     Literal SQL code that gets placed into the command. INSERT {extra} INTO ...
        /// </param>
        /// <returns>
        ///     The number of rows added to the table.
        /// </returns>
        public int Insert(object obj, string extra)
        {
            if (obj == null)
            {
                return 0;
            }

            return Insert(obj, extra, obj.GetType());
        }

        /// <summary>
        ///     Inserts the given object and retrieves its
        ///     auto incremented primary key if it has one.
        /// </summary>
        /// <param name="obj">
        ///     The object to insert.
        /// </param>
        /// <param name="extra">
        ///     Literal SQL code that gets placed into the command. INSERT {extra} INTO ...
        /// </param>
        /// <param name="objType">
        ///     The type of object to insert.
        /// </param>
        /// <returns>
        ///     The number of rows added to the table.
        /// </returns>
        public int Insert(object obj, string extra, Type objType)
        {
            if (obj == null || objType == null)
            {
                return 0;
            }

            TableMapping map = GetMapping(objType);
            if (map.PK != null && map.PK.IsAutoGuid)
            {
                PropertyInfo prop = objType.GetProperty(map.PK.PropertyName);
                if (prop != null) //if (prop.GetValue(obj, null).Equals(Guid.Empty)) {
                {
                    if (prop.GetGetMethod().Invoke(obj, null).Equals(Guid.Empty))
                    {
                        prop.SetValue(obj, Guid.NewGuid(), null);
                    }
                }
            }

            var replacing = string.Compare(extra, "OR REPLACE", StringComparison.OrdinalIgnoreCase) == 0;

            var cols = replacing ? map.InsertOrReplaceColumns : map.InsertColumns;
            var vals = new object[cols.Length];
            for (var i = 0; i < vals.Length; i++)
            {
                vals[i] = cols[i].GetValue(obj);
            }

            PreparedSQLiteInsertCommand insertCmd = map.GetInsertCommand(this, extra);
            int count;

            try
            {
                count = insertCmd.ExecuteNonQuery(vals);
            }
            catch (SQLiteException ex)
                when (ex.ExtendedResult == SQLite3.EExtendedResult.ConstraintNotNull)
            {
                throw SQLiteExceptionFactory.New(ex.Message, ex.Result, map, obj);
            }

            if (map.HasAutoIncPK)
            {
                var id = SQLite3.LastInsertRowId(ConnectionHandle);
                map.SetAutoIncPK(obj, id);
            }

            return count;
        }

        /// <summary>
        ///     Updates all of the columns of a table using the specified object
        ///     except for its primary key.
        ///     The object is required to have a primary key.
        /// </summary>
        /// <param name="obj">
        ///     The object to update. It must have a primary key designated using the PrimaryKeyAttribute.
        /// </param>
        /// <returns>
        ///     The number of rows updated.
        /// </returns>
        public int Update(object obj)
        {
            if (obj == null)
            {
                return 0;
            }

            return Update(obj, obj.GetType());
        }

        /// <summary>
        ///     Updates all of the columns of a table using the specified object
        ///     except for its primary key.
        ///     The object is required to have a primary key.
        /// </summary>
        /// <param name="obj">
        ///     The object to update. It must have a primary key designated using the PrimaryKeyAttribute.
        /// </param>
        /// <param name="objType">
        ///     The type of object to insert.
        /// </param>
        /// <returns>
        ///     The number of rows updated.
        /// </returns>
        public int Update(object obj, Type objType)
        {
            if (obj == null)
            {
                throw new ArgumentNullException(nameof(obj));
            }

            if (objType == null)
            {
                throw new ArgumentNullException(nameof(objType));
            }

            TableMapping map = GetMapping(objType);
            TableMapping.Column primaryKeyColumn = map.PK;
            if (primaryKeyColumn == null)
            {
                throw new NotSupportedException("Cannot update " + map.TableName + ": it has no PK");
            }

            var columns = (from column in map.Columns where column != primaryKeyColumn select column).ToList();
            var values = from column in columns select column.GetValue(obj);
            var ps = new List<object>(values) { primaryKeyColumn.GetValue(obj) };
            var query =
                $"update \"{map.TableName}\" " +
                $"set {string.Join(",", (from c in columns select "\"" + c.Name + "\" = ? ").ToArray())} " +
                $"where {primaryKeyColumn.Name} = ? ";

            int rowsAffected;
            try
            {
                rowsAffected = Execute(query, ps.ToArray());
            }
            catch (SQLiteException ex)
                when (ex.Result == SQLite3.EResult.Constraint && ex.ExtendedResult == SQLite3.EExtendedResult.ConstraintNotNull)
            {
                throw SQLiteExceptionFactory.New("Could not update", ex, map, obj, ConnectionHandle);
            }

            return rowsAffected;
        }

        /// <summary>
        ///     Updates all specified objects.
        /// </summary>
        /// <param name="objects">
        ///     An <see cref="IEnumerable" /> of the objects to insert.
        /// </param>
        /// <returns>
        ///     Total number of update rows.
        /// </returns>
        public int UpdateAll(IEnumerable objects)
        {
            var totalUpdatedRows = 0;
            RunInTransaction(
                () =>
                {
                    foreach (var r in objects)
                    {
                        totalUpdatedRows += Update(r);
                    }
                });
            return totalUpdatedRows;
        }

        /// <summary>
        ///     Deletes the given object from the database using its primary key.
        /// </summary>
        /// <param name="objectToDelete">
        ///     The object to delete. It must have a primary key designated using the PrimaryKeyAttribute.
        /// </param>
        /// <returns>
        ///     The number of rows deleted.
        /// </returns>
        public int Delete(object objectToDelete)
        {
            TableMapping map = GetMapping(objectToDelete.GetType());
            if (map.PK == null)
            {
                throw new NotSupportedException("Cannot delete " + map.TableName + ": it has no PK");
            }

            var sqlQuery = "delete from \"{0}\" where \"{1}\" = ?".Format(map.TableName, map.PK.Name);
            return Execute(sqlQuery, map.PK.GetValue(objectToDelete));
        }

        /// <summary>
        ///     Deletes the object with the specified primary key.
        /// </summary>
        /// <param name="primaryKey">
        ///     The primary key of the object to delete.
        /// </param>
        /// <returns>
        ///     The number of objects deleted.
        /// </returns>
        /// <typeparam name='T'>
        ///     The type of object.
        /// </typeparam>
        public int Delete<T>(object primaryKey)
        {
            TableMapping map = GetMapping<T>();
            if (map.PK == null)
            {
                throw new NotSupportedException($"Cannot delete '{map.TableName}': it has no PK");
            }

            var sqlQuery = "delete from \"{0}\" where \"{1}\" = ?".Format(map.TableName, map.PK.Name);
            return Execute(sqlQuery, primaryKey);
        }

        /// <summary>
        ///     Deletes all the objects from the specified table.
        ///     WARNING WARNING: Let me repeat. It deletes ALL the objects from the
        ///     specified table. Do you really want to do that?
        /// </summary>
        /// <returns>
        ///     The number of objects deleted.
        /// </returns>
        /// <typeparam name='T'>
        ///     The type of objects to delete.
        /// </typeparam>
        public int DeleteAll<T>()
        {
            TableMapping map = GetMapping<T>();
            var query = $"delete from \"{map.TableName}\"";
            return Execute(query);
        }

        ~SQLiteConnection()
        {
            Dispose(EDisposeCaller.Finalizing);
        }

        protected virtual void Dispose(EDisposeCaller disposeCaller)
        {
            if (disposeCaller == EDisposeCaller.Disposing)
            {
                Close();
            }
        }

        public void Close()
        {
            if (!_isOpen || ConnectionHandle == null)
            {
                return;
            }

            try
            {
                _mappings?.Values.ForEach(sqlInsertCommand => sqlInsertCommand.Dispose());
                ConnectionHandle.Dispose();
            }
            finally
            {
                ConnectionHandle = null;
                _isOpen = false;
            }
        }

        private struct IndexedColumn
        {
            public int Order;
            public string ColumnName;
        }

        private struct IndexInfo
        {
            public string IndexName;
            public string TableName;
            public bool Unique;
            public List<IndexedColumn> Columns;
        }

        [PublicAPI, Preserve]
        public class ColumnInfo
        {
//			public int cid { get; set; }

            [Column("name")] public string Name { get; set; }

//			[Column ("type")]
//			public string ColumnType { get; set; }

            public int notnull { get; set; }

//			public string dflt_value { get; set; }

//			public int pk { get; set; }

            public override string ToString()
            {
                return Name;
            }
        }

        #region debug tracing

        public bool Trace { get; set; }
        public bool TimeExecution { get; set; }

        public delegate void TraceHandler(string message);

        public event TraceHandler TraceEvent;

        internal void InvokeTrace(string message)
        {
            TraceEvent?.Invoke(message);
        }

        public delegate void TimeExecutionHandler(TimeSpan executionTime, TimeSpan totalExecutionTime);

        public event TimeExecutionHandler TimeExecutionEvent;

        internal void InvokeTimeExecution(TimeSpan executionTime, TimeSpan totalExecutionTime)
        {
            TimeExecutionEvent?.Invoke(executionTime, totalExecutionTime);
        }

        #endregion debug tracing
    }
}