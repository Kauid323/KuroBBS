using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Data.Json;
using KuroBBS.Helpers;
using KuroBBS.Models;

namespace KuroBBS.Services
{
    public class SmsCodeResult
    {
        public bool Success { get; set; }
        public bool NeedGeetest { get; set; }
        public string CaptchaId { get; set; }
        public string Message { get; set; }
    }

    public class LoginResult
    {
        public bool Success { get; set; }
        public string Token { get; set; }
        public string UserId { get; set; }
        public string UserName { get; set; }
        public string AvatarUrl { get; set; }
        public string Message { get; set; }
    }

    public class KuroAuthService
    {
        public const string DefaultCaptchaId = "ec4aa4174277d822d73f2442a165a2cd";

        private static KuroAuthService _instance;
        public static KuroAuthService Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new KuroAuthService();
                }
                return _instance;
            }
        }

        public async Task<SmsCodeResult> RequestSmsCodeAsync(string mobile, string geeTestData = "")
        {
            var result = new SmsCodeResult { CaptchaId = DefaultCaptchaId };
            if (string.IsNullOrWhiteSpace(mobile))
            {
                result.Message = "请输入有效的手机号码";
                return result;
            }

            var parameters = new Dictionary<string, string>
            {
                { "mobile", mobile.Trim() },
                { "geeTestData", geeTestData ?? "" }
            };

            KuroLogger.Loading("SMS_REQUEST", "Requesting SMS code for mobile " + mobile);
            // /user/getSmsCodeForH5 is an H5-only endpoint. It MUST be called with the
            // official H5 request headers (source=h5); with source=android the server's
            // risk control replies data.geeTest=true and never actually sends the SMS.
            var json = await KuroApiClient.Instance.PostFormAsync("/user/getSmsCodeForH5", parameters, null, h5: true);

            if (json == null)
            {
                result.Message = "网络请求失败，请检查网络连接";
                return result;
            }

            int code = json.ContainsKey("code") ? (int)json.GetNamedNumber("code") : -1;
            string msg = json.ContainsKey("msg") ? json.GetNamedString("msg") : "";
            result.Message = msg;

            if (code == 200)
            {
                if (json.ContainsKey("data") && json.GetNamedValue("data").ValueType == JsonValueType.Object)
                {
                    var data = json.GetNamedObject("data");
                    if (data.ContainsKey("geeTest") && data.GetNamedValue("geeTest").ValueType == JsonValueType.Boolean)
                    {
                        result.NeedGeetest = data.GetNamedBoolean("geeTest");
                    }
                    if (data.ContainsKey("captchaId") && data.GetNamedValue("captchaId").ValueType == JsonValueType.String)
                    {
                        result.CaptchaId = data.GetNamedString("captchaId");
                    }
                }

                // data.geeTest == true  => risk control demands the GT captcha and the
                // SMS has NOT been sent yet. Only geeTest == false means it went out.
                if (!result.NeedGeetest)
                {
                    result.Success = true;
                    KuroLogger.Info("SMS_SENT", "SMS verification code sent successfully to " + mobile);
                }
                else
                {
                    KuroLogger.Loading("SMS_GEETEST", "Geetest risk verification required for " + mobile);
                }
            }
            else
            {
                // If the error indicates geetest required
                if (code == 1001 || msg.Contains("验证") || msg.Contains("风控"))
                {
                    result.NeedGeetest = true;
                }
                KuroLogger.Warn("SMS_FAILED", string.Format("SMS request failed (code {0}): {1}", code, msg));
            }

            return result;
        }

        public async Task<LoginResult> LoginWithSmsAsync(string mobile, string smsCode)
        {
            var result = new LoginResult();
            if (string.IsNullOrWhiteSpace(mobile) || string.IsNullOrWhiteSpace(smsCode))
            {
                result.Message = "请输入手机号和验证码";
                return result;
            }

            var parameters = new Dictionary<string, string>
            {
                { "mobile", mobile.Trim() },
                { "code", smsCode.Trim() },
                { "devCode", SettingsHelper.DevCode }
            };

            KuroLogger.Loading("LOGIN_SMS", "Submitting SMS login for " + mobile);
            var json = await KuroApiClient.Instance.PostFormAsync("/user/sdkLogin", parameters);

            if (json == null)
            {
                result.Message = "网络请求失败，请检查网络连接";
                return result;
            }

            int code = json.ContainsKey("code") ? (int)json.GetNamedNumber("code") : -1;
            string msg = json.ContainsKey("msg") ? json.GetNamedString("msg") : "";
            result.Message = msg;

            if (code == 200 && json.ContainsKey("data") && json.GetNamedValue("data").ValueType == JsonValueType.Object)
            {
                var data = json.GetNamedObject("data");
                result.Success = true;
                result.Token = data.ContainsKey("token") ? data.GetNamedString("token") : "";
                result.UserId = data.ContainsKey("userId") ? data.GetNamedString("userId") : "";
                result.UserName = data.ContainsKey("userName") ? data.GetNamedString("userName") : "";
                result.AvatarUrl = data.ContainsKey("headUrl") ? data.GetNamedString("headUrl") : "";

                // Save to local settings
                SettingsHelper.Token = result.Token;
                SettingsHelper.UserId = result.UserId;
                SettingsHelper.UserName = result.UserName;

                KuroLogger.Info("LOGIN_SUCCESS", string.Format("User {0} (UID: {1}) logged in successfully!", result.UserName, result.UserId));
            }
            else
            {
                KuroLogger.Warn("LOGIN_FAILED", string.Format("Login failed (code {0}): {1}", code, msg));
            }

            return result;
        }

        public async Task<LoginResult> LoginWithTokenOrCookieAsync(string tokenOrCookie)
        {
            var result = new LoginResult();
            if (string.IsNullOrWhiteSpace(tokenOrCookie))
            {
                result.Message = "请输入 Token 或 Cookie";
                return result;
            }

            string token = ExtractToken(tokenOrCookie);
            if (string.IsNullOrEmpty(token))
            {
                result.Message = "未能从输入中解析出有效 Token";
                return result;
            }

            string oldToken = SettingsHelper.Token;
            string oldUid = SettingsHelper.UserId;
            string oldName = SettingsHelper.UserName;

            // Set token temporarily to verify
            SettingsHelper.Token = token;

            KuroLogger.Loading("LOGIN_TOKEN", "正在验证 Token 凭证...");
            var profile = await KuroUserService.Instance.GetUserProfileDetailAsync("");

            if (profile != null && !string.IsNullOrEmpty(profile.UserId) && profile.UserId != "0")
            {
                result.Success = true;
                result.Token = token;
                result.UserId = profile.UserId;
                result.UserName = profile.UserName;
                result.AvatarUrl = profile.AvatarUrl;
                result.Message = "登录成功";

                SettingsHelper.Token = token;
                SettingsHelper.UserId = profile.UserId;
                SettingsHelper.UserName = profile.UserName;

                KuroLogger.Info("LOGIN_SUCCESS", string.Format("Token 验证成功！用户: {0} (UID: {1})", profile.UserName, profile.UserId));
            }
            else
            {
                // Revert
                SettingsHelper.Token = oldToken;
                SettingsHelper.UserId = oldUid;
                SettingsHelper.UserName = oldName;

                result.Message = "Token 校验失败，凭证无效或已过期";
                KuroLogger.Warn("LOGIN_FAILED", "Token 验证失败，接口返回空或未授权");
            }

            return result;
        }

        public static string ExtractToken(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";
            raw = raw.Trim();

            // Try matching "token="
            int idx = raw.IndexOf("token=", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                int start = idx + 6;
                int end = raw.IndexOf(';', start);
                if (end < 0) end = raw.Length;
                string token = raw.Substring(start, end - start).Trim().Trim('"', '\'', ' ');
                return token;
            }

            // Try matching "auth_token="
            int authIdx = raw.IndexOf("auth_token=", StringComparison.OrdinalIgnoreCase);
            if (authIdx >= 0)
            {
                int start = authIdx + 11;
                int end = raw.IndexOf(';', start);
                if (end < 0) end = raw.Length;
                string token = raw.Substring(start, end - start).Trim().Trim('"', '\'', ' ');
                return token;
            }

            // Raw string
            return raw.Trim('"', '\'', ';', ' ');
        }

        public async Task<bool> LogoutAsync()
        {
            try
            {
                await KuroApiClient.Instance.PostFormAsync("/user/sdkLogout", new Dictionary<string, string>());
            }
            catch { }

            SettingsHelper.Token = "";
            SettingsHelper.UserId = "";
            SettingsHelper.UserName = "未登录用户";
            KuroLogger.Info("LOGOUT", "User logged out. Credentials cleared.");
            return true;
        }
    }
}
