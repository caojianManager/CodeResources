namespace FrameWork.Common
{
    public interface IInitializable
    {
        void Init();
    }
    public class Singleton<T> where T : new()
    {
        private static T _instance;
        public static T GetInstance()
        {
            if (_instance == null)
            {
                _instance = new T();

                if (_instance is IInitializable initializable)
                {
                    initializable.Init();
                }
            }
            return _instance;
        }
    }
}
