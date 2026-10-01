using System;
using Windows.Storage;

namespace KuroBBS.Helpers
{
    public static class SettingsHelper
    {
        private static readonly ApplicationDataContainer LocalSettings = ApplicationData.Current.LocalSettings;

        private const string KeyToken = "Kuro_Token";
        private const string KeyUserId = "Kuro_UserId";
        private const string KeyUserName = "Kuro_UserName";
        private const string KeyDevCode = "Kuro_DevCode";
        private const string KeyDefaultGameId = "Kuro_DefaultGameId";
        private const string KeyAllRoleColumns = "Kuro_AllRoleColumns";

        public static string Token
        {
            get { return GetValue(KeyToken, ""); }
            set { SetValue(KeyToken, value); }
        }

        public static string UserId
        {
            get { return GetValue(KeyUserId, ""); }
            set { SetValue(KeyUserId, value); }
        }

        public static string UserName
        {
            get { return GetValue(KeyUserName, "库街区用户"); }
            set { SetValue(KeyUserName, value); }
        }

        public static string DevCode
        {
            get
            {
                var code = GetValue(KeyDevCode, "");
                if (string.IsNullOrEmpty(code))
                {
                    code = Guid.NewGuid().ToString("N");
                    SetValue(KeyDevCode, code);
                }
                return code;
            }
            set { SetValue(KeyDevCode, value); }
        }

        public static int DefaultGameId
        {
            get { return GetValue(KeyDefaultGameId, 3); } // 3 = 鸣潮 (Wuthering Waves), 2 = 战双 (PNS)
            set { SetValue(KeyDefaultGameId, value); }
        }

        public static int AllRoleColumns
        {
            get
            {
                int value = GetValue(KeyAllRoleColumns, 3);
                return value >= 2 && value <= 5 ? value : 3;
            }
            set { SetValue(KeyAllRoleColumns, value >= 2 && value <= 5 ? value : 3); }
        }

        public static bool IsLoggedIn
        {
            get { return !string.IsNullOrEmpty(Token); }
        }

        private static T GetValue<T>(string key, T defaultValue)
        {
            if (LocalSettings.Values.ContainsKey(key))
            {
                return (T)LocalSettings.Values[key];
            }
            return defaultValue;
        }

        private static void SetValue<T>(string key, T value)
        {
            LocalSettings.Values[key] = value;
        }
    }
}
