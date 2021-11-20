using System;
using JetBrains.Annotations;

// Better to store attributes in separate namespace to not conflict with similar
namespace SQLite4Unity3d.Attributes
{
    [PublicAPI]
    [AttributeUsage(AttributeTargets.Class)]
    public class TableAttribute : Attribute
    {
        public TableAttribute(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }

    [PublicAPI]
    [AttributeUsage(AttributeTargets.Property)]
    public class ColumnAttribute : Attribute
    {
        public ColumnAttribute(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }

    [PublicAPI]
    [AttributeUsage(AttributeTargets.Property)]
    public class PrimaryKeyAttribute : Attribute
    {}

    [PublicAPI]
    [AttributeUsage(AttributeTargets.Property)]
    public class AutoIncrementAttribute : Attribute
    {}

    [PublicAPI]
    [AttributeUsage(AttributeTargets.Property)]
    public class IndexedAttribute : Attribute
    {
        public IndexedAttribute()
        {
        }

        public IndexedAttribute(string name, int order)
        {
            Name = name;
            Order = order;
        }

        public string Name { get; }
        public int Order { get; }
        public virtual bool Unique { get; }
    }

    [PublicAPI]
    [AttributeUsage(AttributeTargets.Property)]
    public class IgnoreAttribute : Attribute
    {}

    [PublicAPI]
    [AttributeUsage(AttributeTargets.Property)]
    public class UniqueAttribute : IndexedAttribute
    {
        public UniqueAttribute()
        {
        }

        public UniqueAttribute(string name, int order) : base(name, order)
        {
        }

        public override bool Unique => true;
    }

    [PublicAPI]
    [AttributeUsage(AttributeTargets.Property)]
    public class MaxLengthAttribute : Attribute
    {
        public MaxLengthAttribute(int length)
        {
            Value = length;
        }

        public int Value { get; }
    }

    [PublicAPI]
    [AttributeUsage(AttributeTargets.Property)]
    public class CollationAttribute : Attribute
    {
        public CollationAttribute(string collation)
        {
            Value = collation;
        }

        public string Value { get; }
    }

    [PublicAPI]
    [AttributeUsage(AttributeTargets.Property)]
    public class NotNullAttribute : Attribute
    {}
}