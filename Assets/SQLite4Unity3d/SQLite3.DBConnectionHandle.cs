using System;
using System.Runtime.InteropServices;
using JetBrains.Annotations;
using UnityEngine;

namespace SQLite4Unity3d
{
    public static partial class SQLite3
    {
        [PublicAPI]
        public class DBConnectionHandle : SafeHandle
        {
            public DBConnectionHandle() : base(IntPtr.Zero, true)
            {
            }

            public override bool IsInvalid => handle == IntPtr.Zero;

            public static implicit operator IntPtr(DBConnectionHandle dbConnectionHandle)
            {
                return dbConnectionHandle.handle;
            }

            protected override bool ReleaseHandle()
            {
                if (IsInvalid || IsClosed)
                {
                    return false;
                }

                EResult result = CloseConnection();
                if (result != EResult.OK)
                {
                    CharPtr message = ErrorMessage16(this);
                    Debug.LogError($"Couldn't close database connection. Result: {result}, Error message: {message}");
                    return false;
                }

                handle = IntPtr.Zero;
                return true;
            }

            protected virtual EResult CloseConnection()
            {
                return SQLite3.Close(this);
            }
        }
    }
}