using JetBrains.Annotations;

namespace SQLite4Unity3d
{
    public static partial class SQLite3
    {
        [PublicAPI]
        public class DBConnectionV2Handle : DBConnectionHandle
        {
            protected override EResult CloseConnection()
            {
                return CloseV2(this);
            }
        }
    }
}