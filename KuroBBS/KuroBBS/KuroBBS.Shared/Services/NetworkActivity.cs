using System;
using System.Threading;

namespace KuroBBS.Services
{
    /// <summary>
    /// Tracks active API calls so pages can show one consistent loading state.
    /// </summary>
    public static class NetworkActivity
    {
        private static int _activeRequests;

        public static bool IsBusy
        {
            get { return Interlocked.CompareExchange(ref _activeRequests, 0, 0) > 0; }
        }

        public static event EventHandler ActivityChanged;

        public static IDisposable Begin()
        {
            if (Interlocked.Increment(ref _activeRequests) == 1)
            {
                NotifyChanged();
            }

            return new ActivityScope();
        }

        private static void End()
        {
            int remaining = Interlocked.Decrement(ref _activeRequests);
            if (remaining <= 0)
            {
                Interlocked.Exchange(ref _activeRequests, 0);
                NotifyChanged();
            }
        }

        private static void NotifyChanged()
        {
            var handler = ActivityChanged;
            if (handler != null)
            {
                handler(null, EventArgs.Empty);
            }
        }

        private sealed class ActivityScope : IDisposable
        {
            private int _ended;

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _ended, 1) == 0)
                {
                    End();
                }
            }
        }
    }
}
