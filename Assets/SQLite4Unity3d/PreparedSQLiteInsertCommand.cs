using System;
using JetBrains.Annotations;
using SQLite4Unity3d.Utils;

namespace SQLite4Unity3d
{
    /// <summary>
    ///     Since the insert never changed, we only need to prepare once.
    /// </summary>
    [PublicAPI]
    public class PreparedSQLiteInsertCommand : IDisposable
    {
        private bool _isDisposed;

        internal PreparedSQLiteInsertCommand(SQLiteConnection connection)
        {
            Connection = connection;
        }

        public bool Initialized { get; set; }
        public string CommandText { get; set; }
        protected SQLiteConnection Connection { get; set; }
        protected SQLite3.StatementHandle StatementHandle { get; set; }

        public void Dispose()
        {
            Dispose(EDisposeCaller.Disposing);
            GC.SuppressFinalize(this);
        }

        /// <param name="parameters">Parameters to bind to command</param>
        /// <returns>Affected rows count</returns>
        /// <exception cref="SQLiteException" />
        public int ExecuteNonQuery(object[] parameters)
        {
            if (Connection.Trace)
            {
                Connection.InvokeTrace("Executing: " + CommandText);
            }

            if (!Initialized)
            {
                StatementHandle = PrepareV2();
                Initialized = true;
            }

            for (var i = 0; i < parameters?.Length; i += 1)
            {
                // TODO: Why we pass index as 'i + 1'?
                SQLiteCommand.BindParameter(StatementHandle, i + 1, parameters[i], Connection.StoreDateTimeAsTicks);
            }

            SQLite3.EResult result = SQLite3.Step(StatementHandle);
            if (result != SQLite3.EResult.Done)
            {
                SQLite3.Reset(StatementHandle);
                throw SQLiteExceptionFactory.New("Could not execute non-query", result, Connection.ConnectionHandle);
            }

            var rowsAffected = SQLite3.Changes(Connection.ConnectionHandle);
            SQLite3.Reset(StatementHandle);
            return rowsAffected;
        }

        protected virtual SQLite3.StatementHandle PrepareV2()
        {
            SQLite3.StatementHandle statement = SQLite3.PrepareV2(Connection.ConnectionHandle, CommandText);
            return statement;
        }

        private void Dispose(EDisposeCaller disposeCaller)
        {
            if (_isDisposed)
            {
                return;
            }

            StatementHandle?.Dispose();
            StatementHandle = null;
            Connection = null;
            _isDisposed = true;
        }

        ~PreparedSQLiteInsertCommand()
        {
            Dispose(EDisposeCaller.Finalizing);
        }
    }
}