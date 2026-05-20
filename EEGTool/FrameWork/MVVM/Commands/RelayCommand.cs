
namespace Framework.MVVM.Commands
{
    class RelayCommand : Command
    {
        private Action<object>? _Excute {  get; set; }
        private Predicate<object>? _CanExcute { get; set; }

        public RelayCommand(Action<object> excute, Predicate<object>? canExcute = null)
        {
            _Excute = excute;
            _CanExcute = canExcute;
        }

        public override bool CanExecute(object parameter)
        {
            if (parameter == null)
                return true;
             _CanExcute?.Invoke(parameter);
            return true;
        }

        public override void Execute(object parameter)
        {
            _Excute?.Invoke(parameter);
        }
    }
}
