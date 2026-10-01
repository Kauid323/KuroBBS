#if WINDOWS_PHONE_APP
using Windows.ApplicationModel.Activation;

namespace KuroBBS.Helpers
{
    public interface IFileOpenPickerContinuable
    {
        void ContinueWithFileOpenPicker(FileOpenPickerContinuationEventArgs args);
    }
}
#endif
