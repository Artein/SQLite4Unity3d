namespace SQLite4Unity3d
{
    public static class SQLiteExceptionFactory
    {
        public static SQLiteException New(string message, SQLite3.EResult result)
        {
            return new SQLiteException(message: message, result: result);
        }
        
        public static SQLiteException New(
            string message,
            SQLite3.EResult result,
            SQLite3.DBConnectionHandle connectionHandle)
        {
            if (connectionHandle == null)
            {
                return New(message, result);
            }
            
            var databaseErrorMessage = SQLite3.ErrorMessage16(connectionHandle);
            var extendedErrorCode = SQLite3.ExtendedErrCode(connectionHandle);
            if (result == SQLite3.EResult.Constraint && extendedErrorCode == SQLite3.EExtendedResult.ConstraintNotNull)
            {
                var fullDatabaseErrorMessage = $"[{extendedErrorCode}] {databaseErrorMessage}";
                return new NotNullConstraintViolationException(message, result, fullDatabaseErrorMessage);
            }

            return new SQLiteException(message, result, databaseErrorMessage, extendedErrorCode);
        }

        public static NotNullConstraintViolationException New(
            string message,
            SQLite3.EResult result,
            TableMapping mapping,
            object obj)
        {
            // TODO: check this implementation
            // var databaseErrorMessage = SQLite3.GetErrorMessage(connectionHandle);
            return new NotNullConstraintViolationException(message, result, message, mapping, obj);
        }

        public static NotNullConstraintViolationException New(
            string message,
            SQLiteException exception,
            TableMapping mapping,
            object obj, 
            SQLite3.DBConnectionHandle connectionHandle)
        {
            return new NotNullConstraintViolationException(
                message: message,
                result: exception.Result,
                databaseErrorMessage: SQLite3.ErrorMessage16(connectionHandle),
                mapping: mapping,
                obj: obj);
        }
    }
}