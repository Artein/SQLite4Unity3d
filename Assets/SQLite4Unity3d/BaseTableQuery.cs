namespace SQLite4Unity3d
{
    public abstract class BaseTableQuery
    {
        protected class Ordering
        {
            public string ColumnName { get; set; }
            public bool IsAscending { get; set; }
        }
    }
}