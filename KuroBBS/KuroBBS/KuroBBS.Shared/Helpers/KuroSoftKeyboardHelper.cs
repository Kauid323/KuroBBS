using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace KuroBBS.Helpers
{
    /// <summary>
    /// 软键盘（输入法）控制工具。
    ///
    /// 背景：WP8.1 / UWP 上，只要某个 TextBox 持有焦点，软键盘就会弹出；
    /// 而 ContentDialog / 弹窗关闭时，系统又会把焦点「还原」到上一次聚焦的
    /// 控件——如果那是个搜索框，键盘就会莫名其妙地再弹出来。
    ///
    /// 处理原则很简单：**别让输入框自动持有焦点**。需要时把焦点从输入框上
    /// 移走（交给承载页），键盘自然收起。
    ///
    /// 注意：这里刻意**不用** InputPane.TryHide()。
    /// 本文件是「源码共享」给 KuroBBS.Windows（Win8.1 桌面）与
    /// KuroBBS.WindowsPhone（WP8.1）两个 head 项目的，
    /// 而 Win8.1 桌面版的 InputPane 并没有 TryHide()，会编译失败。
    /// 只靠转移焦点就已足够收起键盘。
    /// </summary>
    public static class KuroSoftKeyboardHelper
    {
        /// <summary>
        /// 把焦点从当前输入控件上移走，从而收起软键盘。
        /// 做法：让承载页（Page 派生自 Control，可成为焦点对象）获得焦点，
        /// 焦点一旦离开 TextBox，输入面板随即收起。
        /// </summary>
        public static void DismissFor(Page page)
        {
            if (page == null) return;

            // 把焦点交给页面本身（非输入控件）→ 键盘收起。
            page.Focus(FocusState.Programmatic);
        }

        /// <summary>
        /// 卸载指定输入控件上的焦点（仅当它当前正持有焦点时）。
        /// 用于「弹窗/菜单 打开前 / 关闭后」把搜索框的焦点卸掉，
        /// 从根本上杜绝系统把焦点还原到搜索框、把键盘顶起来。
        /// </summary>
        public static void ClearFocusIfFocused(Page page, Control input)
        {
            if (page == null || input == null) return;

            if (input.FocusState != FocusState.Unfocused)
            {
                DismissFor(page);
            }
        }
    }
}
