using System.Collections.Generic;
using System.Linq;

namespace SQLite4Unity3d
{
    public class NotNullConstraintViolationException : SQLiteException
    {
        public NotNullConstraintViolationException(
            string message,
            SQLite3.EResult result,
            string databaseErrorMessage = null,
            TableMapping mapping = null,
            object obj = null)
            : base(message, result, databaseErrorMessage)
        {
            if (mapping != null && obj != null)
            {
                Columns = from column in mapping.Columns
                    where column.IsNullable == false && column.GetValue(obj) == null
                    select column;
            }
        }

        public IEnumerable<TableMapping.Column> Columns { get; }
    }
}