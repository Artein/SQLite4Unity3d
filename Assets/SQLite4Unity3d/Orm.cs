using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using SQLite4Unity3d.Attributes;
using NotNullAttribute = JetBrains.Annotations.NotNullAttribute;

namespace SQLite4Unity3d
{
    public static class Orm
    {
        public const string ImplicitPkName = "Id";
        public const string ImplicitIndexSuffix = "Id";

        // TODO: What is "Decl"?
        [PublicAPI]
        public static string SqlDecl(TableMapping.Column column, bool storeDateTimeAsTicks)
        {
            var typeString = GetColumnSqlType(column, storeDateTimeAsTicks);
            var decl = $"\"{column.Name}\" {typeString} ";
            decl += column.IsPK ? "primary key " : string.Empty;
            decl += column.IsAutoInc ? "autoincrement " : string.Empty;
            decl += column.IsNullable ? string.Empty : "not null ";
            decl += string.IsNullOrEmpty(column.Collation) ? string.Empty : $"collate {column.Collation} ";
            return decl;
        }

        [PublicAPI]
        public static string GetColumnSqlType(TableMapping.Column column, bool storeDateTimeAsTicks)
        {
            var columnType = column.ColumnType;

            if (columnType == typeof(uint) || columnType == typeof(long))
            {
                return "bigint";
            }

            if (columnType == typeof(float) || columnType == typeof(double) || columnType == typeof(decimal))
            {
                return "float";
            }

            if (columnType == typeof(string))
            {
                return column.MaxStringLength.HasValue ? "varchar(" + column.MaxStringLength.Value + ")" : "varchar";
            }

            if (columnType == typeof(TimeSpan))
            {
                return "bigint";
            }

            if (columnType == typeof(DateTime))
            {
                return storeDateTimeAsTicks ? "bigint" : "datetime";
            }

            if (columnType == typeof(DateTimeOffset))
            {
                return "bigint";
            }

            if (columnType.IsEnum)
            {
                return "integer";
            }

            if (columnType == typeof(byte[]))
            {
                return "blob";
            }

            if (columnType == typeof(Guid))
            {
                return "varchar(36)";
            }

            if (columnType == typeof(bool) || columnType == typeof(byte) || columnType == typeof(ushort) ||
                columnType == typeof(sbyte) || columnType == typeof(short) || columnType == typeof(int))
            {
                return "integer";
            }

            throw new NotSupportedException($"Implementation does not support '{columnType.Name}' type");
        }

        [PublicAPI]
        public static bool IsPrimaryKey(MemberInfo memberInfo)
        {
            var attributes = memberInfo.GetCustomAttributes(typeof(PrimaryKeyAttribute), true);
            return attributes.Length > 0;
        }

        [PublicAPI]
        public static string GetCollation(MemberInfo memberInfo)
        {
            var attributes = memberInfo.GetCustomAttributes(typeof(CollationAttribute), true);
            return attributes.Length > 0 ? ((CollationAttribute)attributes[0]).Value : string.Empty;
        }

        [PublicAPI]
        public static bool IsAutoIncrement(MemberInfo memberInfo)
        {
            var attributes = memberInfo.GetCustomAttributes(typeof(AutoIncrementAttribute), true);
            return attributes.Length > 0;
        }

        [PublicAPI]
        public static IEnumerable<IndexedAttribute> GetIndices(MemberInfo memberInfo)
        {
            var attributes = memberInfo.GetCustomAttributes(typeof(IndexedAttribute), true);
            return attributes.Cast<IndexedAttribute>();
        }

        [PublicAPI]
        public static int? MaxStringLength(PropertyInfo propertyInfo)
        {
            var attributes = propertyInfo.GetCustomAttributes(typeof(MaxLengthAttribute), true);
            return attributes.Length > 0 ? ((MaxLengthAttribute)attributes[0]).Value : null;
        }

        [PublicAPI]
        public static bool IsNotNull(MemberInfo memberInfo)
        {
            var attributes = memberInfo.GetCustomAttributes(typeof(NotNullAttribute), true);
            return attributes.Length > 0;
        }
    }
}