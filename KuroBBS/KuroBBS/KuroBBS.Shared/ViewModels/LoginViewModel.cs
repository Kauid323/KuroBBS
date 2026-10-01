using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.UI.Xaml;
using KuroBBS.Helpers;
using KuroBBS.Services;

namespace KuroBBS.ViewModels
{
    public class LoginViewModel : ViewModelBase
    {
        private string _mobile = "";
        public string Mobile
        {
            get { return _mobile; }
            set { _mobile = value; OnPropertyChanged(); }
        }

        private string _smsCode = "";
        public string SmsCode
        {
            get { return _smsCode; }
            set { _smsCode = value; OnPropertyChanged(); }
        }

        private string _tokenOrCookieInput = "";
        public string TokenOrCookieInput
        {
            get { return _tokenOrCookieInput; }
            set { _tokenOrCookieInput = value; OnPropertyChanged(); }
        }

        private string _countdownText = "获取验证码";
        public string CountdownText
        {
            get { return _countdownText; }
            set { _countdownText = value; OnPropertyChanged(); }
        }

        private bool _canSendSms = true;
        public bool CanSendSms
        {
            get { return _canSendSms; }
            set { _canSendSms = value; OnPropertyChanged(); }
        }

        public bool HasStatusMessage
        {
            get { return !string.IsNullOrEmpty(StatusMessage); }
        }

        private bool _isGeetestVisible;
        public bool IsGeetestVisible
        {
            get { return _isGeetestVisible; }
            set { _isGeetestVisible = value; OnPropertyChanged(); }
        }

        private string _geetestHtml = "";
        public string GeetestHtml
        {
            get { return _geetestHtml; }
            set { _geetestHtml = value; OnPropertyChanged(); }
        }

        public event EventHandler<string> RequestNavigateGeetest;

        private bool _isStatusSuccess = false;
        public bool IsStatusSuccess
        {
            get { return _isStatusSuccess; }
            set { _isStatusSuccess = value; OnPropertyChanged(); }
        }

        private DispatcherTimer _timer;
        private int _countdownSeconds = 0;

        public ICommand RequestSmsCodeCommand { get; private set; }
        public ICommand LoginWithSmsCommand { get; private set; }
        public ICommand LoginWithTokenCommand { get; private set; }

        public event Action<bool, string> LoginCompleted;

        public LoginViewModel()
        {
            RequestSmsCodeCommand = new RelayCommand(async () => await RequestSmsCodeAsync());
            LoginWithSmsCommand = new RelayCommand(async () => await LoginWithSmsAsync());
            LoginWithTokenCommand = new RelayCommand(async () => await LoginWithTokenAsync());
        }

        public async Task RequestSmsCodeAsync()
        {
            if (IsBusy || !CanSendSms) return;

            string mobile = _mobile != null ? _mobile.Trim() : "";
            if (string.IsNullOrEmpty(mobile) || mobile.Length != 11)
            {
                SetStatus("请输入正确的 11 位手机号码", false);
                return;
            }

            IsBusy = true;
            SetStatus("正在发送短信验证码...", false);

            try
            {
                var result = await KuroAuthService.Instance.RequestSmsCodeAsync(mobile, "");
                if (result.Success)
                {
                    SetStatus("验证码已发送，请注意查收短信", true);
                    StartCountdown(60);
                }
                else if (result.NeedGeetest)
                {
                    TriggerGeetest(result.CaptchaId);
                    SetStatus("请完成极验验证后再发送短信", false);
                    SetStatus("当前账号触发风控验证，建议切换为 Token/Cookie 登录", false);
                }
                else
                {
                    SetStatus(string.IsNullOrEmpty(result.Message) ? "发送验证码失败" : result.Message, false);
                }
            }
            catch (Exception ex)
            {
                SetStatus("发送异常: " + ex.Message, false);
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void TriggerGeetest(string captchaId)
        {
            if (string.IsNullOrEmpty(captchaId)) captchaId = KuroAuthService.DefaultCaptchaId;
            KuroLogger.Loading("GEETEST_OPEN", "Opening Geetest verification on LoginPage (CaptchaId: " + captchaId + ")");
            GeetestHtml = MainViewModel.BuildGeetestHtml(captchaId);
            IsGeetestVisible = true;
            if (RequestNavigateGeetest != null) RequestNavigateGeetest(this, GeetestHtml);
        }

        public async Task OnGeetestValidatedAsync(string validateJson)
        {
            IsGeetestVisible = false;
            IsBusy = true;
            SetStatus("极验通过，正在重新发送短信验证码...", false);
            try
            {
                var result = await KuroAuthService.Instance.RequestSmsCodeAsync(Mobile.Trim(), validateJson);
                if (result.Success)
                {
                    SetStatus("验证码已发送，请注意查收短信", true);
                    StartCountdown(60);
                }
                else if (result.NeedGeetest)
                {
                    SetStatus("验证已失效，请重新完成极验", false);
                    TriggerGeetest(result.CaptchaId);
                }
                else SetStatus(string.IsNullOrEmpty(result.Message) ? "发送验证码失败" : result.Message, false);
            }
            finally { IsBusy = false; }
        }

        public void OnGeetestFailed(string message)
        {
            IsGeetestVisible = false;
            SetStatus("极验验证失败或已取消，请重试", false);
        }

        public async Task LoginWithSmsAsync()
        {
            if (IsBusy) return;

            string mobile = _mobile != null ? _mobile.Trim() : "";
            string code = _smsCode != null ? _smsCode.Trim() : "";

            if (string.IsNullOrEmpty(mobile) || mobile.Length != 11)
            {
                SetStatus("请输入 11 位手机号码", false);
                return;
            }

            if (string.IsNullOrEmpty(code))
            {
                SetStatus("请输入短信验证码", false);
                return;
            }

            IsBusy = true;
            SetStatus("正在验证并登录...", false);

            try
            {
                var result = await KuroAuthService.Instance.LoginWithSmsAsync(mobile, code);
                if (result.Success)
                {
                    SetStatus("登录成功！正在跳转...", true);
                    if (LoginCompleted != null)
                    {
                        LoginCompleted(true, result.UserName);
                    }
                }
                else
                {
                    SetStatus(string.IsNullOrEmpty(result.Message) ? "登录失败，请检查验证码" : result.Message, false);
                }
            }
            catch (Exception ex)
            {
                SetStatus("登录异常: " + ex.Message, false);
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task LoginWithTokenAsync()
        {
            if (IsBusy) return;

            string raw = _tokenOrCookieInput != null ? _tokenOrCookieInput.Trim() : "";
            if (string.IsNullOrEmpty(raw))
            {
                SetStatus("请输入 Token 或 Cookie 内容", false);
                return;
            }

            IsBusy = true;
            SetStatus("正在校验凭证并获取用户信息...", false);

            try
            {
                var result = await KuroAuthService.Instance.LoginWithTokenOrCookieAsync(raw);
                if (result.Success)
                {
                    SetStatus(string.Format("登录成功！欢迎回来，{0}", result.UserName), true);
                    if (LoginCompleted != null)
                    {
                        LoginCompleted(true, result.UserName);
                    }
                }
                else
                {
                    SetStatus(string.IsNullOrEmpty(result.Message) ? "凭证校验失败，请检查后重试" : result.Message, false);
                }
            }
            catch (Exception ex)
            {
                SetStatus("校验异常: " + ex.Message, false);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void SetStatus(string message, bool isSuccess)
        {
            StatusMessage = message;
            IsStatusSuccess = isSuccess;
            OnPropertyChanged("HasStatusMessage");
        }

        private void StartCountdown(int seconds)
        {
            _countdownSeconds = seconds;
            CanSendSms = false;
            CountdownText = string.Format("{0}s", _countdownSeconds);

            if (_timer == null)
            {
                _timer = new DispatcherTimer();
                _timer.Interval = TimeSpan.FromSeconds(1);
                _timer.Tick += (s, e) =>
                {
                    _countdownSeconds--;
                    if (_countdownSeconds <= 0)
                    {
                        _timer.Stop();
                        CanSendSms = true;
                        CountdownText = "重新获取";
                    }
                    else
                    {
                        CountdownText = string.Format("{0}s", _countdownSeconds);
                    }
                };
            }

            _timer.Start();
        }

        public void Cleanup()
        {
            IsGeetestVisible = false;
            if (_timer != null)
            {
                _timer.Stop();
                _timer = null;
            }
        }
    }
}
