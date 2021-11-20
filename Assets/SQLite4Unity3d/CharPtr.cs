using System;
using System.Runtime.InteropServices;
using JetBrains.Annotations;

namespace SQLite4Unity3d
{
    [UsedImplicitly]
    public class CharPtr : SafeHandle, IConvertible
    {
        private string _managedCharBF;

        private CharPtr() : base(IntPtr.Zero, true)
        {
        }

        private string ManagedChar => _managedCharBF ??= Marshal.PtrToStringUni(handle);

        public override bool IsInvalid => _managedCharBF == null;

        public static implicit operator IntPtr(CharPtr charPtr)
        {
            return charPtr.handle;
        }

        public static implicit operator string(CharPtr charPtr)
        {
            return charPtr.ManagedChar;
        }

        public override string ToString()
        {
            return ManagedChar;
        }

        protected override bool ReleaseHandle()
        {
            _managedCharBF = null;
            return true;
        }

        #region IConvertible

        TypeCode IConvertible.GetTypeCode()
        {
            throw new NotImplementedException();
        }

        bool IConvertible.ToBoolean(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        byte IConvertible.ToByte(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        char IConvertible.ToChar(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        DateTime IConvertible.ToDateTime(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        decimal IConvertible.ToDecimal(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        double IConvertible.ToDouble(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        short IConvertible.ToInt16(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        int IConvertible.ToInt32(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        long IConvertible.ToInt64(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        sbyte IConvertible.ToSByte(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        float IConvertible.ToSingle(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        string IConvertible.ToString(IFormatProvider provider)
        {
            return ManagedChar;
        }

        object IConvertible.ToType(Type conversionType, IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        ushort IConvertible.ToUInt16(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        uint IConvertible.ToUInt32(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        ulong IConvertible.ToUInt64(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        #endregion IConvertible
    }
}