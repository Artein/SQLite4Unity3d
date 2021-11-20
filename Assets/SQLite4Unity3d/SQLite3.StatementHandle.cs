using System;
using System.Runtime.InteropServices;
using JetBrains.Annotations;

namespace SQLite4Unity3d
{
    public static partial class SQLite3
    {
        [PublicAPI]
        public class StatementHandle : SafeHandle
        {
            public StatementHandle() : base(IntPtr.Zero, true)
            {
            }

            public override bool IsInvalid => handle == IntPtr.Zero;

            public static implicit operator IntPtr(StatementHandle statementHandle)
            {
                return statementHandle.handle;
            }

            protected override bool ReleaseHandle()
            {
                if (IsInvalid || IsClosed)
                {
                    return false;
                }

                var result = SQLite3.Finalize(this);
                if (result != EResult.OK)
                {
                    throw SQLiteExceptionFactory.New($"Could not release {nameof(StatementHandle)}", result);
                }

                handle = IntPtr.Zero;
                return true;
            }
        }
    }
}