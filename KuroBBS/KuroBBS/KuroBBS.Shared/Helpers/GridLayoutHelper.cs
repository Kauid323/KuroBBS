using System;

namespace KuroBBS.Helpers
{
    /// <summary>
    /// 「每行显示 N 个」网格尺寸计算。设置里的列数要全局生效（全部角色 / 图鉴列表
    /// 含意识手册 / 金币商店），各列表页都走这里，避免每处各写一遍导致不一致。
    ///
    /// 模式参考 AllRoleViewModel：availableWidth 来自 Window.Current.Bounds 或
    /// SizeChangedEventArgs.NewSize（减掉页面 Padding 后），按列数 floor 等分得到单元格宽。
    /// </summary>
    public static class GridLayoutHelper
    {
        /// <summary>单元格宽度 = floor(可用宽 / 列数)，但不低于一个最小值。</summary>
        public static double CalcItemWidth(double availableWidth, int columns, double minWidth)
        {
            int cols = columns < 1 ? 1 : columns;
            if (availableWidth <= 0) return minWidth;
            double w = Math.Floor(availableWidth / cols);
            return w < minWidth ? minWidth : w;
        }

        /// <summary>单元格高度 = floor(宽 * 宽高比)，保证卡片比例一致。</summary>
        public static double CalcItemHeight(double itemWidth, double aspect)
        {
            if (itemWidth <= 0) return 0;
            return Math.Floor(itemWidth * aspect);
        }
    }
}
