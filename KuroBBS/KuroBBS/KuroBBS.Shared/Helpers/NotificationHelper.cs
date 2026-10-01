using System;
using System.Threading.Tasks;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;
using Windows.UI.Popups;
using KuroBBS.Services;

namespace KuroBBS.Helpers
{
    public static class NotificationHelper
    {
        public static void ShowToast(string title, string content)
        {
            try
            {
                var toastXml = ToastNotificationManager.GetTemplateContent(ToastTemplateType.ToastText02);
                var textElements = toastXml.GetElementsByTagName("text");
                if (textElements.Length >= 1)
                {
                    textElements[0].AppendChild(toastXml.CreateTextNode(title ?? "库街区"));
                }
                if (textElements.Length >= 2)
                {
                    textElements[1].AppendChild(toastXml.CreateTextNode(content ?? ""));
                }
                var toast = new ToastNotification(toastXml);
                ToastNotificationManager.CreateToastNotifier().Show(toast);
                KuroLogger.Info("TOAST_SHOWN", string.Format("Toast notification dispatched: [{0}] {1}", title, content));
            }
            catch (Exception ex)
            {
                KuroLogger.Warn("TOAST_ERR", "Failed to show system toast: " + ex.Message);
            }
        }

        public static void ShowNotification(string content, string title = "库街区")
        {
            ShowToast(title, content);
        }

        public static async Task ShowDialogAsync(string content, string title = "库街区")
        {
            try
            {
                var dialog = new MessageDialog(content ?? "", title ?? "库街区");
                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                KuroLogger.Warn("DIALOG_ERR", "Failed to show dialog: " + ex.Message);
            }
        }
    }
}
