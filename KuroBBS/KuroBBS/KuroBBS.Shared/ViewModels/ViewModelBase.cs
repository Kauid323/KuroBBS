using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using KuroBBS.Services;

namespace KuroBBS.ViewModels
{
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        private bool _isBusy;

        protected ViewModelBase()
        {
            NetworkActivity.ActivityChanged += NetworkActivity_ActivityChanged;
        }

        public bool IsBusy
        {
            get { return _isBusy || NetworkActivity.IsBusy; }
            set { _isBusy = value; OnPropertyChanged(); }
        }

        private void NetworkActivity_ActivityChanged(object sender, EventArgs e)
        {
            OnPropertyChanged("IsBusy");
        }

        private string _statusMessage;
        public string StatusMessage
        {
            get { return _statusMessage; }
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            var handler = PropertyChanged;
            if (handler == null) return;

            try
            {
                CoreDispatcher dispatcher = null;
                if (Windows.UI.Xaml.Window.Current != null)
                {
                    dispatcher = Windows.UI.Xaml.Window.Current.Dispatcher;
                }
                else if (CoreApplication.MainView != null && CoreApplication.MainView.CoreWindow != null)
                {
                    dispatcher = CoreApplication.MainView.CoreWindow.Dispatcher;
                }

                if (dispatcher != null && !dispatcher.HasThreadAccess)
                {
                    var ignore = dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        var h = PropertyChanged;
                        if (h != null)
                        {
                            h(this, new PropertyChangedEventArgs(propertyName));
                        }
                    });
                    return;
                }
            }
            catch
            {
            }

            handler(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
