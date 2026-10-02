using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace KuroBBS.Helpers
{
    /// <summary>
    /// 软键盘（输入法）控制工具 —— **仅 Windows Phone 8.1 head 使用**。
    ///
    /// 为什么放在 KuroBBS.WindowsPhone 而不是 KuroBBS.Shared：
    /// 这段逻辑依赖 WP 专有的交互习惯（Back 键收起键盘、避免系统把焦点还原到搜索框）。
    /// 之前放在 Shared 里，Shared 是「源码共享」给两个 head 的，
    /// 桌面版（KuroBBS.Windows）并没有这些需求，也缺少对应 API（如 InputPane.TryHide），
    /// 结果一改 Shared 就顺手把桌面版编崩。
    /// WP 专属适配代码一律留在本 head 项目内。
    ///
    /// 处理原则很简单：**别让输入框自动持有焦点**。
    /// 需要时把焦点从输入框上移走（交给承载页），键盘自然收起——
    /// 刻意不调 InputPane.TryHide()，只靠转移焦点就已足够。
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
