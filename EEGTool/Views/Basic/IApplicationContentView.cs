

namespace EEGTool.Views.Basics
{
    public interface IApplicationContentView
    {
        string Name { get; }
        bool IsInit { get; set; }
        bool IsSelected { get; set; }
        void Init();
        void OnHide();
        void OnShow();
    }
}
