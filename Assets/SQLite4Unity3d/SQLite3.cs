//
// Copyright (c) 2009-2012 Krueger Systems, Inc.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//

using System;
using System.Runtime.InteropServices;
using System.Text;
using JetBrains.Annotations;

namespace SQLite4Unity3d
{
    public static partial class SQLite3
    {
        [PublicAPI]
        public enum EColumnType
        {
            Integer = 1,
            Float = 2,
            Text = 3,
            Blob = 4,
            Null = 5,
        }

        [PublicAPI]
        public enum EConfigOption
        {
            SingleThread = 1,
            MultiThread = 2,
            Serialized = 3,
        }

        [PublicAPI]
        [Flags]
        public enum ECreateFlags
        {
            None = 0,
            ImplicitPrimaryKey = 1, // create a primary key for field called 'Id' (Orm.ImplicitPkName)
            ImplicitIndex = 2, // create an index for fields ending in 'Id' (Orm.ImplicitIndexSuffix)
            AllImplicit = 3, // do both above
            AutoIncrementPrimaryKey = 4, // force PK field to be auto inc
        }

        [PublicAPI]
        public enum EExtendedResult
        {
            IOErrorRead = EResult.IOError | (1 << 8),
            IOErrorShortRead = EResult.IOError | (2 << 8),
            IOErrorWrite = EResult.IOError | (3 << 8),
            IOErrorFsync = EResult.IOError | (4 << 8),
            IOErrorDirFSync = EResult.IOError | (5 << 8),
            IOErrorTruncate = EResult.IOError | (6 << 8),
            IOErrorFStat = EResult.IOError | (7 << 8),
            IOErrorUnlock = EResult.IOError | (8 << 8),
            IOErrorRdlock = EResult.IOError | (9 << 8),
            IOErrorDelete = EResult.IOError | (10 << 8),
            IOErrorBlocked = EResult.IOError | (11 << 8),
            IOErrorNoMem = EResult.IOError | (12 << 8),
            IOErrorAccess = EResult.IOError | (13 << 8),
            IOErrorCheckReservedLock = EResult.IOError | (14 << 8),
            IOErrorLock = EResult.IOError | (15 << 8),
            IOErrorClose = EResult.IOError | (16 << 8),
            IOErrorDirClose = EResult.IOError | (17 << 8),
            IOErrorSHMOpen = EResult.IOError | (18 << 8),
            IOErrorSHMSize = EResult.IOError | (19 << 8),
            IOErrorSHMLock = EResult.IOError | (20 << 8),
            IOErrorSHMMap = EResult.IOError | (21 << 8),
            IOErrorSeek = EResult.IOError | (22 << 8),
            IOErrorDeleteNoEnt = EResult.IOError | (23 << 8),
            IOErrorMMap = EResult.IOError | (24 << 8),
            LockedSharedCache = EResult.Locked | (1 << 8),
            BusyRecovery = EResult.Busy | (1 << 8),
            CannotOpenNoTempDir = EResult.CannotOpen | (1 << 8),
            CannotOpenIsDir = EResult.CannotOpen | (2 << 8),
            CannotOpenFullPath = EResult.CannotOpen | (3 << 8),
            CorruptVTab = EResult.Corrupt | (1 << 8),
            ReadonlyRecovery = EResult.ReadOnly | (1 << 8),
            ReadonlyCannotLock = EResult.ReadOnly | (2 << 8),
            ReadonlyRollback = EResult.ReadOnly | (3 << 8),
            AbortRollback = EResult.Abort | (2 << 8),
            ConstraintCheck = EResult.Constraint | (1 << 8),
            ConstraintCommitHook = EResult.Constraint | (2 << 8),
            ConstraintForeignKey = EResult.Constraint | (3 << 8),
            ConstraintFunction = EResult.Constraint | (4 << 8),
            ConstraintNotNull = EResult.Constraint | (5 << 8),
            ConstraintPrimaryKey = EResult.Constraint | (6 << 8),
            ConstraintTrigger = EResult.Constraint | (7 << 8),
            ConstraintUnique = EResult.Constraint | (8 << 8),
            ConstraintVTab = EResult.Constraint | (9 << 8),
            NoticeRecoverWAL = EResult.Notice | (1 << 8),
            NoticeRecoverRollback = EResult.Notice | (2 << 8),
        }

        [PublicAPI]
        [Flags]
        public enum EOpenFlags
        {
            ReadOnly = 1,
            ReadWrite = 2,
            Create = 4,
            NoMutex = 0x8000,
            FullMutex = 0x10000,
            SharedCache = 0x20000,
            PrivateCache = 0x40000,
            ProtectionComplete = 0x00100000,
            ProtectionCompleteUnlessOpen = 0x00200000,
            ProtectionCompleteUntilFirstUserAuthentication = 0x00300000,
            ProtectionNone = 0x00400000,
        }

        [PublicAPI]
        public enum EResult
        {
            OK = 0,
            Error = 1,
            Internal = 2,
            Perm = 3,
            Abort = 4,
            Busy = 5,
            Locked = 6,
            NoMem = 7,
            ReadOnly = 8,
            Interrupt = 9,
            IOError = 10,
            Corrupt = 11,
            NotFound = 12,
            Full = 13,
            CannotOpen = 14,
            LockErr = 15,
            Empty = 16,
            SchemaChanged = 17,
            TooBig = 18,
            Constraint = 19,
            Mismatch = 20,
            Misuse = 21,
            NotImplementedLFS = 22,
            AccessDenied = 23,
            Format = 24,
            Range = 25,
            NonDBFile = 26,
            Notice = 27,
            Warning = 28,
            Row = 100,
            Done = 101,
        }

        /// <see href="https://www.sqlite.org/c3ref/open.html"/>
        [PublicAPI]
        [DllImport(DLLName, EntryPoint = "sqlite3_open", CallingConvention = SQLiteCC)]
        public static extern EResult Open(
            [MarshalAs(UnmanagedType.LPStr)] string dbFilenameUTF8,
            out DBConnectionHandle connectionHandle);

        /// <see href="https://www.sqlite.org/c3ref/open.html"/>
        [PublicAPI]
        [DllImport(DLLName, EntryPoint = "sqlite3_open16", CallingConvention = SQLiteCC)]
        public static extern EResult Open16(
            [MarshalAs(UnmanagedType.LPWStr)] string dbFilenameUTF16,
            out DBConnectionHandle connectionHandle);

        /// <see href="https://www.sqlite.org/c3ref/open.html"/>
        [PublicAPI]
        [DllImport(DLLName, EntryPoint = "sqlite3_open_v2", CallingConvention = SQLiteCC)]
        public static extern EResult OpenV2(
            [MarshalAs(UnmanagedType.LPStr)] string dbFilenameUTF8,
            out DBConnectionV2Handle connectionHandle,
            int flags, // TODO: Add named flags (as Result enum) https://www.sqlite.org/c3ref/c_open_autoproxy.html
            [MarshalAs(UnmanagedType.LPStr)] string vfsModuleName); // name of VFS module to use

        /// <see href="https://www.sqlite.org/c3ref/open.html"/>
        /// Open using the byte[], when the path may include Unicode. To convert use string.ToNullTerminatedUTF8()
        [PublicAPI]
        [DllImport(DLLName, EntryPoint = "sqlite3_open_v2", CallingConvention = SQLiteCC)]
        public static extern EResult OpenV2(
            byte[] filename,
            out DBConnectionV2Handle connectionHandle,
            int flags, // TODO: Add named flags (as Result enum) https://www.sqlite.org/c3ref/c_open_autoproxy.html
            [MarshalAs(UnmanagedType.LPStr)] string vfsModuleName); // name of VFS module to use

        /// <see href="https://www.sqlite.org/c3ref/enable_load_extension.html"/>
        [PublicAPI]
        [DllImport(DLLName, EntryPoint = "sqlite3_enable_load_extension", CallingConvention = SQLiteCC)]
        public static extern EResult EnableLoadExtension(DBConnectionHandle connectionHandle, int enabled);

        /// <see href="https://www.sqlite.org/c3ref/close.html"/>
        [PublicAPI]
        [DllImport(DLLName, EntryPoint = "sqlite3_close", CallingConvention = SQLiteCC)]
        public static extern EResult Close(DBConnectionHandle connectionHandle);

        /// <see href="https://www.sqlite.org/c3ref/close.html"/>
        [PublicAPI]
        [DllImport(DLLName, EntryPoint = "sqlite3_close_v2", CallingConvention = SQLiteCC)]
        public static extern EResult CloseV2(DBConnectionV2Handle connectionHandle);

        /// <see href="https://www.sqlite.org/c3ref/initialize.html"/>
        [PublicAPI]
        [DllImport(DLLName, EntryPoint = "sqlite3_initialize", CallingConvention = SQLiteCC)]
        public static extern EResult Initialize();

        /// <see href="https://www.sqlite.org/c3ref/initialize.html"/>
        [PublicAPI]
        [DllImport(DLLName, EntryPoint = "sqlite3_shutdown", CallingConvention = SQLiteCC)]
        public static extern EResult Shutdown();

        /// <see href="https://www.sqlite.org/c3ref/config.html"/>
        /// <remarks>
        ///     The sqlite3_config() interface is not thread-safe. The application must ensure
        ///     that no other SQLite interfaces are invoked by other threads while sqlite3_config() is running.
        /// </remarks>
        /// TODO: Add support for additional options https://www.sqlite.org/c3ref/c_config_covering_index_scan.html
        [PublicAPI]
        [DllImport(DLLName, EntryPoint = "sqlite3_config", CallingConvention = SQLiteCC)]
        public static extern EResult Config(EConfigOption option);

#if UNITY_STANDALONE_WIN
        /// <see href="https://www.sqlite.org/c3ref/win32_set_directory.html"/>
        [PublicAPI]
        [DllImport(
            DLLName,
            EntryPoint = "sqlite3_win32_set_directory",
            CallingConvention = CallConv,
            CharSet = CharSet.Unicode)]
        public static extern int SetDirectory(uint directoryType, string directoryPath);

        /// <see href="https://www.sqlite.org/c3ref/win32_set_directory.html"/>
        [PublicAPI]
        [DllImport(
            DLLName,
            EntryPoint = "sqlite3_win32_set_directory8",
            CallingConvention = CallConv,
            CharSet = CharSet.Unicode)]
        public static extern int SetDirectory8(
            uint directoryType,
            [MarshalAs(UnmanagedType.LPStr)] string directoryPathUTF8);

        /// <see href="https://www.sqlite.org/c3ref/win32_set_directory.html"/>
        [PublicAPI]
        [DllImport(
            DLLName,
            EntryPoint = "sqlite3_win32_set_directory16",
            CallingConvention = CallConv,
            CharSet = CharSet.Unicode)]
        public static extern int SetDirectory16(
            uint directoryType,
            [MarshalAs(UnmanagedType.LPWStr)] string directoryPathUTF16);
#endif // UNITY_STANDALONE_WIN

        /// <see href="https://www.sqlite.org/c3ref/busy_timeout.html"/>
        [PublicAPI]
        [DllImport(DLLName, EntryPoint = "sqlite3_busy_timeout", CallingConvention = SQLiteCC)]
        public static extern EResult BusyTimeout(DBConnectionHandle connectionHandle, int milliseconds);

        /// <see href="https://www.sqlite.org/c3ref/changes.html"/>
        [PublicAPI]
        [DllImport(DLLName, EntryPoint = "sqlite3_changes", CallingConvention = SQLiteCC)]
        public static extern int Changes(DBConnectionHandle connectionHandle);

        /// <see href="https://www.sqlite.org/c3ref/prepare.html"/>
        [PublicAPI]
        [DllImport(DLLName, EntryPoint = "sqlite3_prepare_v2", CallingConvention = SQLiteCC)]
        public static extern EResult PrepareV2(
            DBConnectionHandle connectionHandle,
            [MarshalAs(UnmanagedType.LPStr)] string sqlStatementUTF8,
            int sqlStatementUTF8LengthInBytes,
            out StatementHandle statementHandle,
            out IntPtr pzTail); // TODO: Pointer (const char**) to unused portion of sqlStatementUTF8

        [PublicAPI]
        public static StatementHandle PrepareV2(DBConnectionHandle connectionHandle, string sqlStatementUTF8)
        {
            var queryBytesCount = Encoding.UTF8.GetByteCount(sqlStatementUTF8);
            var result = PrepareV2(connectionHandle, sqlStatementUTF8, queryBytesCount, out var statement, out _);
            if (result != EResult.OK)
            {
                throw SQLiteExceptionFactory.New("Could not prepare statement", result, connectionHandle);
            }

            return statement;
        }

        // TODO: int sqlite3_prepare_v3(sqlite3 *db, const char *zSql, int nByte, unsigned int prepFlags, sqlite3_stmt **ppStmt, const char **pzTail);
        // TODO: int sqlite3_prepare16_v2(sqlite3 *db, const void *zSql, int nByte, sqlite3_stmt **ppStmt, const void **pzTail);
        // TODO: int sqlite3_prepare16_v3(sqlite3 *db, const void *zSql, int nByte, unsigned int prepFlags, sqlite3_stmt **ppStmt, const void **pzTail);

        /// <see href="https://www.sqlite.org/c3ref/step.html"/>
        [PublicAPI]
        [DllImport(DLLName, EntryPoint = "sqlite3_step", CallingConvention = SQLiteCC)]
        public static extern EResult Step(StatementHandle statementHandle);

        /// <see href="https://www.sqlite.org/c3ref/reset.html"/>
        [PublicAPI]
        [DllImport(DLLName, EntryPoint = "sqlite3_reset", CallingConvention = SQLiteCC)]
        public static extern EResult Reset(StatementHandle statementHandle);

        /// <see href="https://www.sqlite.org/c3ref/finalize.html"/>
        [PublicAPI]
        [DllImport(DLLName, EntryPoint = "sqlite3_finalize", CallingConvention = SQLiteCC)]
        public static extern EResult Finalize(StatementHandle statementHandle);

        /// <see href="https://www.sqlite.org/c3ref/last_insert_rowid.html"/>
        [PublicAPI]
        [DllImport(DLLName, EntryPoint = "sqlite3_last_insert_rowid", CallingConvention = SQLiteCC)]
        public static extern long LastInsertRowId(DBConnectionHandle connectionHandle);

        /// <see href="https://www.sqlite.org/c3ref/bind_parameter_index.html"/>
        [PublicAPI]
        [DllImport(DLLName, EntryPoint = "sqlite3_bind_parameter_index", CallingConvention = SQLiteCC)]
        public static extern int BindParameterIndex(
            StatementHandle statementHandle,
            [MarshalAs(UnmanagedType.LPStr)] string name);

        /// <see href="https://www.sqlite.org/c3ref/bind_blob.html"/>
        [PublicAPI]
        [DllImport(DLLName, EntryPoint = "sqlite3_bind_blob", CallingConvention = SQLiteCC)]
        public static extern int BindBlob(
            IntPtr databaseHandle,
            int index,
            byte[] blob,
            int blobSize,
            IntPtr free);

        // TODO: int sqlite3_bind_blob64(sqlite3_stmt*, int, const void*, sqlite3_uint64, void(*)(void*));

        /// <see href="https://www.sqlite.org/c3ref/bind_blob.html"/>
        [PublicAPI]
        [DllImport(DLLName, EntryPoint = "sqlite3_bind_double", CallingConvention = SQLiteCC)]
        public static extern int BindDouble(StatementHandle statementHandle, int index, double value);

        /// <see href="https://www.sqlite.org/c3ref/bind_blob.html"/>
        [PublicAPI]
        [DllImport(DLLName, EntryPoint = "sqlite3_bind_int", CallingConvention = SQLiteCC)]
        public static extern int BindInt(StatementHandle statementHandle, int index, int value);

        /// <see href="https://www.sqlite.org/c3ref/bind_blob.html"/>
        [PublicAPI]
        [DllImport(DLLName, EntryPoint = "sqlite3_bind_int64", CallingConvention = SQLiteCC)]
        public static extern int BindInt64(StatementHandle statementHandle, int index, long value);

        /// <see href="https://www.sqlite.org/c3ref/bind_blob.html"/>
        [PublicAPI]
        [DllImport(DLLName, EntryPoint = "sqlite3_bind_null", CallingConvention = SQLiteCC)]
        public static extern int BindNull(StatementHandle statementHandle, int index);

        /// <see href="https://www.sqlite.org/c3ref/bind_blob.html"/>
        [PublicAPI]
        [DllImport(DLLName, EntryPoint = "sqlite3_bind_text", CallingConvention = SQLiteCC, CharSet = CharSet.Ansi)]
        public static extern int BindText(
            StatementHandle statementHandle,
            int index,
            [MarshalAs(UnmanagedType.LPStr)] string text,
            int textSize,
            IntPtr free);

        /// <see href="https://www.sqlite.org/c3ref/bind_blob.html"/>
        [PublicAPI]
        [DllImport(
            DLLName,
            EntryPoint = "sqlite3_bind_text16",
            CallingConvention = SQLiteCC,
            CharSet = CharSet.Unicode)]
        public static extern int BindText16(
            StatementHandle statementHandle,
            int index,
            [MarshalAs(UnmanagedType.LPWStr)] string text,
            int textSize,
            IntPtr free);

        // TODO: int sqlite3_bind_text64(sqlite3_stmt*, int, const char*, sqlite3_uint64, void(*)(void*), unsigned char encoding);
        // TODO: int sqlite3_bind_value(sqlite3_stmt*, int, const sqlite3_value*);
        // TODO: int sqlite3_bind_pointer(sqlite3_stmt*, int, void*, const char*, void(*)(void*));
        // TODO: int sqlite3_bind_zeroblob(sqlite3_stmt*, int, int n);
        // TODO: int sqlite3_bind_zeroblob64(sqlite3_stmt*, int, sqlite3_uint64);

        /// <see href="https://www.sqlite.org/c3ref/column_count.html"/>
        [PublicAPI]
        [DllImport(DLLName, EntryPoint = "sqlite3_column_count", CallingConvention = SQLiteCC)]
        public static extern int ColumnCount(StatementHandle statementHandle);

        /// <see href="https://www.sqlite.org/c3ref/column_name.html"/>
        [PublicAPI]
        [DllImport(DLLName, EntryPoint = "sqlite3_column_name", CallingConvention = SQLiteCC)]
        public static extern CharPtr ColumnName(StatementHandle statementHandle, int index);

        /// <see href="https://www.sqlite.org/c3ref/column_name.html"/>
        [PublicAPI]
        [DllImport(DLLName, EntryPoint = "sqlite3_column_name16", CallingConvention = SQLiteCC)]
        public static extern CharPtr ColumnName16(StatementHandle statementHandle, int index);

        /// <see href="https://www.sqlite.org/c3ref/column_blob.html"/>
        [PublicAPI]
        [DllImport(DLLName, EntryPoint = "sqlite3_column_blob", CallingConvention = SQLiteCC)]
        public static extern IntPtr ColumnBlob(StatementHandle statementHandle, int index);

        /// <see href="https://www.sqlite.org/c3ref/column_blob.html"/>
        [PublicAPI]
        [DllImport(DLLName, EntryPoint = "sqlite3_column_double", CallingConvention = SQLiteCC)]
        public static extern double ColumnDouble(StatementHandle statementHandle, int index);

        /// <see href="https://www.sqlite.org/c3ref/column_blob.html"/>
        [PublicAPI]
        [DllImport(DLLName, EntryPoint = "sqlite3_column_int", CallingConvention = SQLiteCC)]
        public static extern int ColumnInt(StatementHandle statementHandle, int index);

        /// <see href="https://www.sqlite.org/c3ref/column_blob.html"/>
        [PublicAPI]
        [DllImport(DLLName, EntryPoint = "sqlite3_column_int64", CallingConvention = SQLiteCC)]
        public static extern long ColumnInt64(StatementHandle statementHandle, int index);

        /// <see href="https://www.sqlite.org/c3ref/column_blob.html"/>
        [PublicAPI]
        [DllImport(DLLName, EntryPoint = "sqlite3_column_text", CallingConvention = SQLiteCC)]
        public static extern CharPtr ColumnText(StatementHandle statementHandle, int index);

        /// <see href="https://www.sqlite.org/c3ref/column_blob.html"/>
        [PublicAPI]
        [DllImport(DLLName, EntryPoint = "sqlite3_column_text16", CallingConvention = SQLiteCC)]
        public static extern CharPtr ColumnText16(StatementHandle statementHandle, int index);

        /// <see href="https://www.sqlite.org/c3ref/column_blob.html"/>
        [PublicAPI]
        [DllImport(DLLName, EntryPoint = "sqlite3_column_bytes", CallingConvention = SQLiteCC)]
        public static extern int ColumnBytes(StatementHandle statementHandle, int index);

        // TODO: int sqlite3_column_bytes16(sqlite3_stmt*, int iCol);

        /// <see href="https://www.sqlite.org/c3ref/column_blob.html"/>
        [PublicAPI]
        [DllImport(DLLName, EntryPoint = "sqlite3_column_type", CallingConvention = SQLiteCC)]
        public static extern EColumnType ColumnType(StatementHandle statementHandle, int index);

        [PublicAPI]
        public static byte[] ColumnByteArray(StatementHandle statementHandle, int index)
        {
            var length = ColumnBytes(statementHandle, index);
            var result = new byte[length];
            if (length > 0)
            {
                Marshal.Copy(ColumnBlob(statementHandle, index), result, 0, length);
            }

            return result;
        }

        /// <see href="https://www.sqlite.org/c3ref/errcode.html"/>
        /// <see href="https://www.sqlite.org/rescode.html"/>
        [PublicAPI]
        [DllImport(DLLName, EntryPoint = "sqlite3_errcode", CallingConvention = SQLiteCC)]
        public static extern EResult ErrCode(DBConnectionHandle connectionHandle);

        /// <see href="https://www.sqlite.org/c3ref/errcode.html"/>
        /// <see href="https://www.sqlite.org/rescode.html"/>
        [PublicAPI]
        [DllImport(DLLName, EntryPoint = "sqlite3_extended_errcode", CallingConvention = SQLiteCC)]
        public static extern EExtendedResult ExtendedErrCode(DBConnectionHandle connectionHandle);

        /// <see href="https://www.sqlite.org/c3ref/errcode.html"/>
        [PublicAPI]
        [DllImport(DLLName, EntryPoint = "sqlite3_errmsg", CallingConvention = SQLiteCC)]
        public static extern CharPtr ErrorMessage(DBConnectionHandle connectionHandle);

        /// <see href="https://www.sqlite.org/c3ref/errcode.html"/>
        [PublicAPI]
        [DllImport(DLLName, EntryPoint = "sqlite3_errmsg16", CallingConvention = SQLiteCC)]
        public static extern CharPtr ErrorMessage16(DBConnectionHandle connectionHandle);

        /// <see href="https://www.sqlite.org/c3ref/libversion.html"/>
        [PublicAPI]
        [DllImport(DLLName, EntryPoint = "sqlite3_libversion_number", CallingConvention = SQLiteCC)]
        public static extern int LibVersionNumber();

        private const string DLLName = "sqlite3";

        // ReSharper disable once InconsistentNaming
        private const CallingConvention SQLiteCC = CallingConvention.Cdecl;
    }
}