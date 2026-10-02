using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

// The Blank Application template is documented at http://go.microsoft.com/fwlink/?LinkId=234227

namespace KuroBBS
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public sealed partial class App : Application
    {
#if WINDOWS_PHONE_APP
        private TransitionCollection transitions;
#endif

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
            this.Suspending += this.OnSuspending;
            this.UnhandledException += App_UnhandledException;

            // 兜底：未被观察的 Task 异常默认会在 GC 时被静默吞掉，这里显式记录以便排查。
            // （WP8.1 不暴露 AppDomain.CurrentDomain.UnhandledException 事件，故不用它。）
            try
            {
                System.Threading.Tasks.TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
            }
            catch { }

            // 内存压力监控：WP8.1 内存吃紧时 XAML 布局可能直接 fail-fast（无托管异常、exit code 1）。
            // 记录内存事件，便于判断「闪退」是否源于 OOM。
            // 注意：整个 Windows.System.MemoryManager 类型只存在于 WP8.1，
            // Win8.1 桌面 head 完全没有该类型 → 必须用 WINDOWS_PHONE_APP 条件编译隔离。
#if WINDOWS_PHONE_APP
            try
            {
                Windows.System.MemoryManager.AppMemoryUsageIncreased += MemoryManager_AppMemoryUsageIncreased;
                Windows.System.MemoryManager.AppMemoryUsageLimitChanging += MemoryManager_AppMemoryUsageLimitChanging;
            }
            catch { }
#endif

#if WINDOWS_PHONE_APP
            Windows.Phone.UI.Input.HardwareButtons.BackPressed += HardwareButtons_BackPressed;
#endif

            KuroBBS.Services.KuroLogger.Loading("APP_INIT", "KuroBBS Windows Phone 8.1 Application Initializing...");
            KuroBBS.Services.KuroLogger.ThreadInfo("Main UI Thread initialized", "Thread Access Ready");

            // 【关键】确保 LocalFolder\kuro.log 已创建并写入首行。
            // 之前日志只走 Debug.WriteLine，脱离调试器就没有任何记录，
            // 造成「闪退且无日志」。现在会落盘，且每行独立 flush。
            // （此处同步等待启动期短任务，可接受。）
            var ignoreInit = System.Threading.Tasks.Task.Run(() =>
            {
                KuroBBS.Services.KuroLogger.EnsureFileCreated();
                KuroBBS.Services.KuroLogger.Trace("APP_LAUNCHED");
            });
        }

        /// <summary>
        /// 未被观察的 Task 异常：默认会在 GC 时静默吞掉，这里显式记录以便排查。
        /// </summary>
        private void TaskScheduler_UnobservedTaskException(object sender, System.Threading.Tasks.UnobservedTaskExceptionEventArgs e)
        {
            try
            {
                if (e != null && e.Exception != null)
                {
                    KuroBBS.Services.KuroLogger.Error("TASK_UNOBSERVED", e.Exception.ToString());
                    e.SetObserved();
                }
            }
            catch { }
        }

        /// <summary>
        /// 内存使用上升事件：记录当前用量/上限，用于判断闪退是否与 OOM 相关。
        /// 仅 WP8.1 head 提供 Windows.System.MemoryManager。
        /// </summary>
#if WINDOWS_PHONE_APP
        private void MemoryManager_AppMemoryUsageIncreased(object sender, object e)
        {
            try
            {
                var usage = Windows.System.MemoryManager.AppMemoryUsage;
                var level = Windows.System.MemoryManager.AppMemoryUsageLevel;
                KuroBBS.Services.KuroLogger.Trace(string.Format(
                    "MEM_USAGE_INCREASED usage={0} bytes level={1}", usage, level));
            }
            catch { }
        }

        /// <summary>
        /// 内存上限变化事件：逼近上限时记录（WP8.1 超限会直接杀进程）。
        /// 仅 WP8.1 head 存在该事件与参数类型，故用条件编译隔离。
        /// </summary>
        private void MemoryManager_AppMemoryUsageLimitChanging(object sender, Windows.System.AppMemoryUsageLimitChangingEventArgs e)
        {
            try
            {
                KuroBBS.Services.KuroLogger.Trace(string.Format(
                    "MEM_LIMIT_CHANGING old={0} new={1} current={2}",
                    e.OldLimit, e.NewLimit, Windows.System.MemoryManager.AppMemoryUsage));
            }
            catch { }
        }
#endif

#if WINDOWS_PHONE_APP
        private void HardwareButtons_BackPressed(object sender, Windows.Phone.UI.Input.BackPressedEventArgs e)
        {
            if (e.Handled) return;

            if (!KuroBBS.Helpers.BackPressHelper.CanHandleBackPress())
            {
                e.Handled = true;
                return;
            }

            Frame rootFrame = Window.Current.Content as Frame;
            if (rootFrame != null && rootFrame.CanGoBack)
            {
                e.Handled = true;
                rootFrame.GoBack();
                KuroBBS.Services.KuroLogger.Loading("NAV_BACK", "Hardware Back Button pressed -> Successfully navigated back.");
            }
        }
#endif

        private void App_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            string detail = e != null && e.Exception != null ? e.Exception.ToString() : "(no exception object)";
            KuroBBS.Services.KuroLogger.Error("UNHANDLED_EXCEPTION", (e != null ? e.Message : "unknown") + "\n" + detail);
            KuroBBS.Services.KuroLogger.Trace("UNHANDLED_EXCEPTION_HANDLED message=" + (e != null ? e.Message : ""));

            // 【重要】不要设置 e.Handled = true。
            // 之前这里把它置为 true，会导致：① 真正的致命异常被吞掉，程序继续在半损坏状态下运行；
            // ② 后续行为不可预测（有时表现为静默闪退）。
            // 保持 false，让异常正常冒泡（调试器能断下并给出完整堆栈），日志也已落盘。
            if (e != null)
            {
                e.Handled = false;
            }
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used when the application is launched to open a specific file, to display
        /// search results, and so forth.
        /// </summary>
        /// <param name="e">Details about the launch request and process.</param>
        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
#if DEBUG
            if (System.Diagnostics.Debugger.IsAttached)
            {
                this.DebugSettings.EnableFrameRateCounter = true;
            }
#endif

            Frame rootFrame = Window.Current.Content as Frame;

            // Do not repeat app initialization when the Window already has content,
            // just ensure that the window is active
            if (rootFrame == null)
            {
                // Create a Frame to act as the navigation context and navigate to the first page
                rootFrame = new Frame();

                // TODO: change this value to a cache size that is appropriate for your application
                rootFrame.CacheSize = 1;

                if (e.PreviousExecutionState == ApplicationExecutionState.Terminated)
                {
                    // TODO: Load state from previously suspended application
                }

                // Place the frame in the current Window
                Window.Current.Content = rootFrame;
            }

            if (rootFrame.Content == null)
            {
#if WINDOWS_PHONE_APP
                // Removes the turnstile navigation for startup.
                if (rootFrame.ContentTransitions != null)
                {
                    this.transitions = new TransitionCollection();
                    foreach (var c in rootFrame.ContentTransitions)
                    {
                        this.transitions.Add(c);
                    }
                }

                rootFrame.ContentTransitions = null;
                rootFrame.Navigated += this.RootFrame_FirstNavigated;
#endif

                // When the navigation stack isn't restored navigate to the first page,
                // configuring the new page by passing required information as a navigation
                // parameter
                if (!rootFrame.Navigate(typeof(MainPage), e.Arguments))
                {
                    throw new Exception("Failed to create initial page");
                }
            }

            // Ensure the current window is active
            Window.Current.Activate();
        }

#if WINDOWS_PHONE_APP
        /// <summary>
        /// Restores the content transitions after the app has launched.
        /// </summary>
        /// <param name="sender">The object where the handler is attached.</param>
        /// <param name="e">Details about the navigation event.</param>
        private void RootFrame_FirstNavigated(object sender, NavigationEventArgs e)
        {
            var rootFrame = sender as Frame;
            rootFrame.ContentTransitions = this.transitions ?? new TransitionCollection() { new NavigationThemeTransition() };
            rootFrame.Navigated -= this.RootFrame_FirstNavigated;
        }

        protected override void OnActivated(IActivatedEventArgs args)
        {
            base.OnActivated(args);

            var filePickerArgs = args as FileOpenPickerContinuationEventArgs;
            if (filePickerArgs != null)
            {
                Frame rootFrame = Window.Current.Content as Frame;
                if (rootFrame != null)
                {
                    var page = rootFrame.Content as KuroBBS.Helpers.IFileOpenPickerContinuable;
                    if (page != null)
                    {
                        page.ContinueWithFileOpenPicker(filePickerArgs);
                    }
                }
            }
        }
#endif

        /// <summary>
        /// Invoked when application execution is being suspended.  Application state is saved
        /// without knowing whether the application will be terminated or resumed with the contents
        /// of memory still intact.
        /// </summary>
        /// <param name="sender">The source of the suspend request.</param>
        /// <param name="e">Details about the suspend request.</param>
        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();

            // TODO: Save application state and stop any background activity
            deferral.Complete();
        }
    }
}