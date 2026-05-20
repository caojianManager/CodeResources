
namespace Framework.Event
{
    /// <summary>
    /// 消息分发
    /// </summary>
    /// <typeparam name="Type"></typeparam>
    public class EventUtil<Type>
    {
        Dictionary<Type, Delegate> _eventMap;

        public EventUtil()
        {
            _eventMap = new Dictionary<Type, Delegate>();
        }
        /// <summary>
        /// 添加事件监听
        /// </summary>
        /// <param name="eventName"></param>
        /// <param name="eventHandle"></param>
        public void AddEvent(Type eventName, Action eventHandle)
        {
            if (!_eventMap.ContainsKey(eventName))
                _eventMap[eventName] = (Action)eventHandle;
            else
            {
                try
                {
                    _eventMap[eventName] = Delegate.Combine((Action)_eventMap[eventName], eventHandle);
                }
                catch
                {
                    Console.WriteLine("AddEvent == null, eventName is " + eventName.ToString());
                }
            }
        }

        /// <summary>
        /// 添加事件监听
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="eventName"></param>
        /// <param name="eventHandle"></param>
        public void AddEvent<T>(Type eventName, Action<T> eventHandle)
        {
            if (!_eventMap.ContainsKey(eventName))
                _eventMap[eventName] = eventHandle;
            else
                _eventMap[eventName] = (Action<T>)Delegate.Combine((Action<T>)_eventMap[eventName], eventHandle);
        }
        /// <summary>
        /// 添加事件监听
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="U"></typeparam>
        /// <param name="eventName"></param>
        /// <param name="eventHandle"></param>
        public void AddEvent<T, U>(Type eventName, Action<T, U> eventHandle)
        {
            if (!_eventMap.ContainsKey(eventName))
                _eventMap[eventName] = eventHandle;
            else
                _eventMap[eventName] = Delegate.Combine(_eventMap[eventName], eventHandle);
        }
        /// <summary>
        /// 添加事件监听
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="U"></typeparam>
        /// <typeparam name="V"></typeparam>
        /// <param name="eventName"></param>
        /// <param name="eventHandle"></param>
        public void AddEvent<T, U, V>(Type eventName, Action<T, U, V> eventHandle)
        {
            if (!_eventMap.ContainsKey(eventName))
                _eventMap[eventName] = eventHandle;
            else
                _eventMap[eventName] = Delegate.Combine(_eventMap[eventName], eventHandle);
        }
        /// <summary>
        /// 添加事件监听
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="U"></typeparam>
        /// <typeparam name="V"></typeparam>
        /// <typeparam name="W"></typeparam>
        /// <param name="eventName"></param>
        /// <param name="eventHandle"></param>
        public void AddEvent<T, U, V, W>(Type eventName, Action<T, U, V, W> eventHandle)
        {
            if (!_eventMap.ContainsKey(eventName))
                _eventMap[eventName] = eventHandle;
            else
                _eventMap[eventName] = Delegate.Combine(_eventMap[eventName], eventHandle);
        }
        /// <summary>
        /// 事件发生
        /// </summary>
        /// <param name="eventName"></param>
        public virtual void OnEvent(Type eventName)
        {
            if (_eventMap.ContainsKey(eventName))
            {
                Delegate gata = _eventMap[eventName];
                if (gata == null)
                    return;
                Delegate[] gateList = gata.GetInvocationList();
                int count = gateList.Length;
                for (int i = 0; i < count; i++)
                {
                    string str = gateList[i].Target.ToString();
                    if (str == "null")
                    {
                        _eventMap[eventName] = gata = Delegate.Remove(gata, gateList[i]);
                        Console.WriteLine("gateList[i].Target == null, eventName is " + eventName.ToString());
                        continue;
                    }

                    Action action = gateList[i] as Action;
                    if (action != null)
                    {
#if NeedTry
                        try
                        {
                            action();
                        }
                        catch (Exception ex)
                        {
                            DebugTools.DebugHelper.LogError("action run error, eventName is " + eventName.ToString() + "ex is " + ex);
                        }
#else
                        action();
#endif
                    }
                }
            }
        }
        /// <summary>
        /// 事件发生
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="eventName"></param>
        /// <param name="arg1"></param>
        public virtual void OnEvent<T>(Type eventName, T arg1)
        {
            if (_eventMap.ContainsKey(eventName))
            {
                Delegate gata = _eventMap[eventName];
                if (gata == null)
                    return;
                Delegate[] gateList = gata.GetInvocationList();
                int count = gateList.Length;
                for (int i = 0; i < count; i++)
                {
                    string str = gateList[i].Target.ToString();
                    if (str == "null")
                    {
                        _eventMap[eventName] = gata = Delegate.Remove(gata, gateList[i]);
                        Console.WriteLine("gateList[i].Target == null, eventName is " + eventName.ToString());

                        continue;
                    }
                    Action<T> action = gateList[i] as Action<T>;
                    if (action != null)
                    {
#if NeedTry
                        try
                        {
                            action(arg1);
                        }
                        catch (Exception ex)
                        {
                            DebugTools.DebugHelper.LogError( "action run error, eventName is " + eventName.ToString() + "ex is " + ex);
                        }
#else
                        action(arg1);
#endif
                    }

                }
            }
        }
        /// <summary>
        /// 事件发生
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="U"></typeparam>
        /// <param name="eventName"></param>
        /// <param name="arg1"></param>
        /// <param name="arg2"></param>
        public virtual void OnEvent<T, U>(Type eventName, T arg1, U arg2)
        {
            if (_eventMap.ContainsKey(eventName))
            {
                Delegate gata = _eventMap[eventName];
                if (gata == null)
                    return;
                Delegate[] gateList = gata.GetInvocationList();
                int count = gateList.Length;
                for (int i = 0; i < count; i++)
                {
                    string str = gateList[i].Target.ToString();
                    if (str == "null")
                    {
                        _eventMap[eventName] = gata = Delegate.Remove(gata, gateList[i]);
                        Console.WriteLine("gateList[i].Target == null, eventName is " + eventName.ToString());
                        continue;
                    }
                    Action<T, U> action = gateList[i] as Action<T, U>;
                    if (action != null)
                    {
#if NeedTry
                        try
                        {
                            action(arg1, arg2);
                        }
                        catch (Exception ex)
                        {
                            DebugTools.DebugHelper.LogError("action run error, eventName is " + eventName.ToString() + "ex is " + ex);
                        }
#else
                        action(arg1, arg2);
#endif
                    }
                }
            }
        }
        /// <summary>
        /// 事件发生
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="U"></typeparam>
        /// <typeparam name="V"></typeparam>
        /// <param name="eventName"></param>
        /// <param name="arg1"></param>
        /// <param name="arg2"></param>
        /// <param name="arg3"></param>
        public virtual void OnEvent<T, U, V>(Type eventName, T arg1, U arg2, V arg3)
        {
            if (_eventMap.ContainsKey(eventName))
            {
                Delegate gata = _eventMap[eventName];
                if (gata == null)
                    return;
                Delegate[] gateList = gata.GetInvocationList();
                int count = gateList.Length;
                for (int i = 0; i < count; i++)
                {
                    string str = gateList[i].Target.ToString();
                    if (str == "null")
                    {
                        _eventMap[eventName] = gata = Delegate.Remove(gata, gateList[i]);
                        Console.WriteLine("gateList[i].Target == null, eventName is " + eventName.ToString());
                        continue;
                    }
                    Action<T, U, V> action = gateList[i] as Action<T, U, V>;
                    if (action != null)
                    {
#if NeedTry
                        try
                        {
                            action(arg1, arg2, arg3);
                        }
                        catch (Exception ex)
                        {
                            DebugTools.DebugHelper.LogError("action run error, eventName is " + eventName.ToString() + "ex is " + ex);
                        }
#else
                        action(arg1, arg2, arg3);
#endif
                    }
                }
            }
        }
        /// <summary>
        /// 事件发生
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="U"></typeparam>
        /// <typeparam name="V"></typeparam>
        /// <typeparam name="W"></typeparam>
        /// <param name="eventName"></param>
        /// <param name="arg1"></param>
        /// <param name="arg2"></param>
        /// <param name="arg3"></param>
        /// <param name="arg4"></param>
        public void OnEvent<T, U, V, W>(Type eventName, T arg1, U arg2, V arg3, W arg4)
        {
            if (_eventMap.ContainsKey(eventName))
            {
                Delegate gata = _eventMap[eventName];
                if (gata == null)
                    return;
                Delegate[] gateList = gata.GetInvocationList();
                int count = gateList.Length;
                for (int i = 0; i < count; i++)
                {
                    string str = gateList[i].Target.ToString();
                    if (str == "null")
                    {
                        _eventMap[eventName] = gata = Delegate.Remove(gata, gateList[i]);
                        continue;
                    }
                    Action<T, U, V, W> action = gateList[i] as Action<T, U, V, W>;
                    if (action != null)
                    {
#if NeedTry
                        try
                        {
                            action(arg1, arg2, arg3, arg4);
                        }
                        catch (Exception ex)
                        {
                            DebugTools.DebugHelper.LogError( "action run error, eventName is " + eventName.ToString() + "ex is " + ex);
                        }
#else
                        action(arg1, arg2, arg3, arg4);
#endif
                    }
                }
            }
        }

        public void RemoveEvent(Type eventName)
        {
            if (_eventMap.ContainsKey(eventName))
            {
                _eventMap.Remove(eventName);
            }
        }
        /// <summary>
        /// 移除事件
        /// </summary>
        /// <param name="eventName"></param>
        /// <param name="eventHandle"></param>
        public void RemoveEvent(Type eventName, Action eventHandle)
        {
            Delegate gate = null;
            if (_eventMap.TryGetValue(eventName, out gate))
                _eventMap[eventName] = Delegate.Remove(gate, eventHandle);
        }
        /// <summary>
        /// 移除事件
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="eventName"></param>
        /// <param name="eventHandle"></param>
        public void RemoveEvent<T>(Type eventName, Action<T> eventHandle)
        {
            Delegate gate = null;
            if (_eventMap.TryGetValue(eventName, out gate))
                _eventMap[eventName] = Delegate.Remove(gate, eventHandle);
        }
        /// <summary>
        /// 移除事件
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="U"></typeparam>
        /// <param name="eventName"></param>
        /// <param name="eventHandle"></param>
        public void RemoveEvent<T, U>(Type eventName, Action<T, U> eventHandle)
        {
            Delegate gate = null;
            if (_eventMap.TryGetValue(eventName, out gate))
                _eventMap[eventName] = Delegate.Remove(gate, eventHandle);
        }
        /// <summary>
        /// 移除事件
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="U"></typeparam>
        /// <typeparam name="V"></typeparam>
        /// <param name="eventName"></param>
        /// <param name="eventHandle"></param>
        public void RemoveEvent<T, U, V>(Type eventName, Action<T, U, V> eventHandle)
        {
            Delegate gate = null;
            if (_eventMap.TryGetValue(eventName, out gate))
                _eventMap[eventName] = Delegate.Remove(gate, eventHandle);
        }
        /// <summary>
        /// 移除事件
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="U"></typeparam>
        /// <typeparam name="V"></typeparam>
        /// <typeparam name="W"></typeparam>
        /// <param name="eventName"></param>
        /// <param name="eventHandle"></param>
        public void RemoveEvent<T, U, V, W>(Type eventName, Action<T, U, V, W> eventHandle)
        {
            Delegate gate = null;
            if (_eventMap.TryGetValue(eventName, out gate))
                _eventMap[eventName] = Delegate.Remove(gate, eventHandle);
        }
        /// <summary>
        /// 清除全部事件监听
        /// </summary>
        public void Clear()
        {
            _eventMap.Clear();
        }
    }
}
