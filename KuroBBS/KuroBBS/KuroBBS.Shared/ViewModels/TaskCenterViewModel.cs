using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using KuroBBS.Models;
using KuroBBS.Services;

namespace KuroBBS.ViewModels
{
    /// <summary>
    /// 任务中心：成长任务 / 每日任务 + 金币流水。
    /// 数据来自 /encourage/level/getTaskProcess 与 /encourage/gold/getGoldLogs。
    /// </summary>
    public class TaskCenterViewModel : ViewModelBase
    {
        public ObservableCollection<ShopTaskItem> GrowTasks { get; private set; }
        public ObservableCollection<ShopTaskItem> DailyTasks { get; private set; }
        public ObservableCollection<GoldLogItem> GoldLogs { get; private set; }

        private int _goldNum;
        public int GoldNum
        {
            get { return _goldNum; }
            set { _goldNum = value; OnPropertyChanged(); OnPropertyChanged("GoldText"); }
        }
        public string GoldText { get { return _goldNum.ToString(); } }

        private int _currentDailyGold;
        public int CurrentDailyGold
        {
            get { return _currentDailyGold; }
            set { _currentDailyGold = value; OnPropertyChanged(); OnPropertyChanged("DailyGoldText"); }
        }

        private int _maxDailyGold;
        public int MaxDailyGold
        {
            get { return _maxDailyGold; }
            set { _maxDailyGold = value; OnPropertyChanged(); OnPropertyChanged("DailyGoldText"); }
        }

        public string DailyGoldText { get { return _currentDailyGold + " / " + _maxDailyGold; } }

        public bool HasGrowTasks { get { return GrowTasks.Count > 0; } }
        public bool HasDailyTasks { get { return DailyTasks.Count > 0; } }
        public bool HasLogs { get { return GoldLogs.Count > 0; } }

        /// <summary>gameId：0 表示不区分游戏（与 [627] 真实请求一致）。</summary>
        private int _gameId;
        public int GameId
        {
            get { return _gameId; }
            set { _gameId = value; OnPropertyChanged(); }
        }

        public TaskCenterViewModel()
        {
            GrowTasks = new ObservableCollection<ShopTaskItem>();
            DailyTasks = new ObservableCollection<ShopTaskItem>();
            GoldLogs = new ObservableCollection<GoldLogItem>();
        }

        public async Task LoadAsync(int gameId)
        {
            GameId = gameId;
            StatusMessage = "";
            IsBusy = true;
            try
            {
                var goldTask = KuroShopService.Instance.GetTotalGoldAsync();
                var taskTask = KuroShopService.Instance.GetTaskProcessAsync(gameId, null);
                // type=1 为收入流水（真实抓取里 type=1/2 都用过，这里展示收入）
                var logTask = KuroShopService.Instance.GetGoldLogsAsync(1, 1, 20);

                await taskTask;
                await goldTask;
                await logTask;

                GoldNum = goldTask.Result;

                var process = taskTask.Result;
                CurrentDailyGold = process.CurrentDailyGold;
                MaxDailyGold = process.MaxDailyGold;

                GrowTasks.Clear();
                foreach (var t in process.GrowTask) GrowTasks.Add(t);
                DailyTasks.Clear();
                foreach (var t in process.DailyTask) DailyTasks.Add(t);

                GoldLogs.Clear();
                var logs = logTask.Result;
                if (logs != null)
                {
                    foreach (var l in logs) GoldLogs.Add(l);
                }

                OnPropertyChanged("HasGrowTasks");
                OnPropertyChanged("HasDailyTasks");
                OnPropertyChanged("HasLogs");
            }
            catch (Exception ex)
            {
                StatusMessage = "加载失败：" + ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
