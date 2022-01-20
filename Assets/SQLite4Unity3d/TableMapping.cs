using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SQLite4Unity3d.Attributes;

namespace SQLite4Unity3d
{
    public class TableMapping
    {
        private readonly Column _autoPk;
        private Column[] _insertColumns;

        private PreparedSQLiteInsertCommand _insertCommand;
        private string _insertCommandExtra;
        private Column[] _insertOrReplaceColumns;

        public TableMapping(Type type, SQLite3.ECreateFlags createFlags = SQLite3.ECreateFlags.None)
        {
            MappedType = type;

            var tableAttr = (TableAttribute)type.GetCustomAttributes(typeof(TableAttribute), true).FirstOrDefault();
            TableName = tableAttr != null ? tableAttr.Name : MappedType.Name;

            var props = MappedType.GetProperties(BindingFlags.Public | BindingFlags.Instance |
                                                 BindingFlags.SetProperty);
            var cols = new List<Column>();
            foreach (var p in props)
            {
                var ignore = p.GetCustomAttributes(typeof(IgnoreAttribute), true).Length > 0;
                if (p.CanWrite && !ignore) cols.Add(new Column(p, createFlags));
            }

            Columns = cols.ToArray();
            foreach (var c in Columns)
            {
                if (c.IsAutoInc && c.IsPK) _autoPk = c;
                if (c.IsPK) PK = c;
            }

            HasAutoIncPK = _autoPk != null;

            GetByPrimaryKeySql = PK != null
                ? $"select * from \"{TableName}\" where \"{PK.Name}\" = ?"
                : $"select * from \"{TableName}\" limit 1";
        }

        public Type MappedType { get; }

        public string TableName { get; }

        public Column[] Columns { get; }

        public Column PK { get; }

        public string GetByPrimaryKeySql { get; }

        public bool HasAutoIncPK { get; }

        public Column[] InsertColumns
        {
            get { return _insertColumns ??= Columns.Where(c => !c.IsAutoInc).ToArray(); }
        }

        public Column[] InsertOrReplaceColumns => _insertOrReplaceColumns ??= Columns.ToArray();

        public void SetAutoIncPK(object obj, long id)
        {
            _autoPk?.SetValue(obj, Convert.ChangeType(id, _autoPk.ColumnType, null));
        }

        public Column FindColumnWithPropertyName(string propertyName)
        {
            var exact = Columns.FirstOrDefault(c => c.PropertyName == propertyName);
            return exact;
        }

        public Column FindColumn(string columnName)
        {
            var exact = Columns.FirstOrDefault(c => c.Name == columnName);
            return exact;
        }

        public PreparedSQLiteInsertCommand GetInsertCommand(SQLiteConnection conn, string extra)
        {
            if (_insertCommand == null)
            {
                _insertCommand = CreateInsertCommand(conn, extra);
                _insertCommandExtra = extra;
            }
            else if (_insertCommandExtra != extra)
            {
                _insertCommand.Dispose();
                _insertCommand = CreateInsertCommand(conn, extra);
                _insertCommandExtra = extra;
            }

            return _insertCommand;
        }

        private PreparedSQLiteInsertCommand CreateInsertCommand(SQLiteConnection conn, string extra)
        {
            var cols = InsertColumns;
            string insertSql;
            if (!cols.Any() && Columns.Count() == 1 && Columns[0].IsAutoInc)
            {
                insertSql = string.Format("insert {1} into \"{0}\" default values", TableName, extra);
            }
            else
            {
                var replacing = string.Compare(extra, "OR REPLACE", StringComparison.OrdinalIgnoreCase) == 0;

                if (replacing) cols = InsertOrReplaceColumns;

                insertSql = string.Format("insert {3} into \"{0}\"({1}) values ({2})", TableName,
                    string.Join(",", (from c in cols
                        select "\"" + c.Name + "\"").ToArray()),
                    string.Join(",", (from c in cols
                        select "?").ToArray()), extra);
            }

            var insertCommand = new PreparedSQLiteInsertCommand(conn);
            insertCommand.CommandText = insertSql;
            return insertCommand;
        }

        protected internal void Dispose()
        {
            _insertCommand?.Dispose();
            _insertCommand = null;
        }

        public class Column
        {
            private readonly PropertyInfo _prop;

            public Column(PropertyInfo prop, SQLite3.ECreateFlags createFlags = SQLite3.ECreateFlags.None)
            {
                var colAttr = (ColumnAttribute)prop.GetCustomAttributes(typeof(ColumnAttribute), true).FirstOrDefault();

                _prop = prop;
                Name = colAttr == null ? prop.Name : colAttr.Name;
                //If this type is Nullable<T> then Nullable.GetUnderlyingType returns the T, otherwise it returns null, so get the actual type instead
                ColumnType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                Collation = Orm.GetCollation(prop);

                IsPK = Orm.IsPrimaryKey(prop) ||
                       (createFlags & SQLite3.ECreateFlags.ImplicitPrimaryKey) == SQLite3.ECreateFlags.ImplicitPrimaryKey &&
                       string.Compare(prop.Name, Orm.ImplicitPkName, StringComparison.OrdinalIgnoreCase) == 0;

                var isAuto = Orm.IsAutoIncrement(prop) ||
                             IsPK && (createFlags & SQLite3.ECreateFlags.AutoIncrementPrimaryKey) == SQLite3.ECreateFlags.AutoIncrementPrimaryKey;
                IsAutoGuid = isAuto && ColumnType == typeof(Guid);
                IsAutoInc = isAuto && !IsAutoGuid;

                Indices = Orm.GetIndices(prop);
                if (!Indices.Any()
                    && !IsPK
                    && (createFlags & SQLite3.ECreateFlags.ImplicitIndex) == SQLite3.ECreateFlags.ImplicitIndex
                    && Name.EndsWith(Orm.ImplicitIndexSuffix, StringComparison.OrdinalIgnoreCase)
                )
                    Indices = new IndexedAttribute[] { new() };
                IsNullable = !(IsPK || Orm.IsNotNull(prop));
                MaxStringLength = Orm.MaxStringLength(prop);
            }

            public string Name { get; }

            public string PropertyName => _prop.Name;

            public Type ColumnType { get; }

            public string Collation { get; }

            public bool IsAutoInc { get; }
            public bool IsAutoGuid { get; }

            public bool IsPK { get; }

            public IEnumerable<IndexedAttribute> Indices { get; set; }

            public bool IsNullable { get; }

            public int? MaxStringLength { get; }

            public void SetValue(object obj, object value)
            {
                if (value != null && _prop.PropertyType != value.GetType())
                {
                    value = Convert.ChangeType(value, _prop.PropertyType);
                }

                _prop.SetValue(obj, value, null);
            }

            public object GetValue(object obj)
            {
                var getMethodInfo = _prop.GetGetMethod();
                return getMethodInfo.Invoke(obj, null);
            }
        }
    }
}