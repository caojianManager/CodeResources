

namespace FrameWork.Common
{
    public class SimpleIoC
    {
        private readonly Dictionary<Type, Type> _registrations = new Dictionary<Type, Type>();
        private readonly Dictionary<Type, object> _singletons = new Dictionary<Type, object>();

        // 注册一个类型，默认是每次解析都会创建新的实例
        public void Register<TInterface, TImplementation>() where TImplementation : TInterface
        {
            _registrations[typeof(TInterface)] = typeof(TImplementation);
        }

        // 注册为单例模式，确保每次解析返回同一个实例
        public void RegisterSingleton<TInterface, TImplementation>() where TImplementation : TInterface
        {
            _registrations[typeof(TInterface)] = typeof(TImplementation);
            _singletons[typeof(TInterface)] = null; // 延迟初始化
        }

        public TInterface Resolve<TInterface>()
        {
            Type interfaceType = typeof(TInterface);

            // 检查是否已注册该类型
            if (!_registrations.ContainsKey(interfaceType))
            {
                throw new InvalidOperationException($"未注册类型: {interfaceType.Name}");
            }

            // 如果是单例，直接返回单例对象
            if (_singletons.ContainsKey(interfaceType))
            {
                if (_singletons[interfaceType] == null) // 如果单例还没被创建，创建它
                {
                    Type impType = _registrations[interfaceType];
                    _singletons[interfaceType] = Activator.CreateInstance(impType);
                }
                return (TInterface)_singletons[interfaceType];
            }

            // 非单例，直接创建新的实例
            Type implementationType = _registrations[interfaceType];
            return (TInterface)Activator.CreateInstance(implementationType);
        }

        public TInterface? TryResolve<TInterface>() where TInterface : class
        {
            Type interfaceType = typeof(TInterface);

            if (!_registrations.ContainsKey(interfaceType))
            {
                return null;
            }

            try
            {
                // 如果是单例
                if (_singletons.ContainsKey(interfaceType))
                {
                    if (_singletons[interfaceType] == null)
                    {
                        Type impType = _registrations[interfaceType];
                        _singletons[interfaceType] = Activator.CreateInstance(impType);
                    }
                    return (TInterface)_singletons[interfaceType];
                }

                // 非单例
                Type implementationType = _registrations[interfaceType];
                return (TInterface)Activator.CreateInstance(implementationType);
            }
            catch
            {
                return null; // 构造失败也返回 null
            }
        }

    }
}