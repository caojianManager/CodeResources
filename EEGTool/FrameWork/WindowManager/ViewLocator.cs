using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Interop;

namespace Framework
{
    public static class ViewLocator
    {
        private static readonly Dictionary<Type, object> ViewCache = new();
        private static readonly Dictionary<Type, Type> CustomViewMappings = new();

        public static void RegisterViewMapping<TViewModel, TView>()
        {
            CustomViewMappings[typeof(TViewModel)] = typeof(TView);
        }

        public static object LocateForModel(object model)
        {
            Type viewModelType = model.GetType();

            if (ViewCache.TryGetValue(viewModelType, out var cachedView))
            {
                if (cachedView is Window cachedWindow)
                {
                    var handle = new WindowInteropHelper(cachedWindow).Handle;
                    if (handle != IntPtr.Zero)
                        return cachedWindow;
                    else
                        ViewCache.Remove(viewModelType); // 已关闭，清除缓存
                }
                else
                {
                    return cachedView;
                }
            }

            // 先使用自定义映射
            Type viewType = null;
            if (CustomViewMappings.TryGetValue(viewModelType, out var mappedType))
            {
                viewType = mappedType;
            }
            else
            {
                string viewTypeName = viewModelType.Name.Replace("ViewModel", "View");
                viewType = Assembly.GetExecutingAssembly()
                    .GetTypes()
                    .FirstOrDefault(t => t.Name == viewTypeName);
            }

            if (viewType == null)
            {
                throw new InvalidOperationException($"无法找到与视图模型 {viewModelType.Name} 匹配的视图类型.");
            }

            var viewInstance = Activator.CreateInstance(viewType)

                               ?? throw new InvalidOperationException("视图实例创建失败");

            if (viewInstance is not Window)
            {
                ViewCache[viewModelType] = viewInstance;
            }

            return viewInstance;
        }

        public static void ClearCache()
        {
            ViewCache.Clear();
        }

        public static void ClearCache(object model)
        {
            Type viewModelType = model.GetType();
            ViewCache.Remove(viewModelType);
        }

        public static object? GetViewFromCache(Type viewModelType)
        {
            ViewCache.TryGetValue(viewModelType, out var view);
            return view;
        }
    }
}