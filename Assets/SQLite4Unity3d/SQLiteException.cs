using System;
using UnityEngine;

namespace SQLite4Unity3d
{
    public class SQLiteException : Exception
    {
        public SQLiteException(
            string message, 
            SQLite3.EResult result, 
            string databaseErrorMessage = null, 
            SQLite3.EExtendedResult? extendedResult = null)
            : base(message)
        {
            Result = result;
            ExtendedResult = extendedResult;
            DatabaseErrorMessage = databaseErrorMessage;
            
            Debug.LogException(this);
        }

        public SQLite3.EResult Result { get; }
        public SQLite3.EExtendedResult? ExtendedResult { get; }
        public string DatabaseErrorMessage { get; }
    }
}