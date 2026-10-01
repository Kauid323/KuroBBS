using System;

namespace KuroBBS.Helpers
{
    public static class BackPressHelper
    {
        private static DateTime _lastBackPressTime = DateTime.MinValue;

        /// <summary>
        /// Debounces hardware back press events to prevent double navigation.
        /// Returns true if the back press can be processed.
        /// </summary>
        public static bool CanHandleBackPress()
        {
            var now = DateTime.UtcNow;
            if ((now - _lastBackPressTime).TotalMilliseconds < 350)
            {
                return false;
            }
            _lastBackPressTime = now;
            return true;
        }
    }
}
