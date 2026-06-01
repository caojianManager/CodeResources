using EEGTool;
using FrameWork.Log;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace EegAcquisitionSystem
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        private static Mutex? _appMutex;
        private static bool _mutexReleased;
        private static bool _isExiting;
        private const string MutexName = "EEGTool_Mutex";
        public static Action? ExitEvt;

        protected override void OnStartup(StartupEventArgs e)
        {

            // 手动初始化 log4net，确保最早执行
            log4net.Config.XmlConfigurator.Configure();

            // 设置全局异常捕获
            this.DispatcherUnhandledException += OnDispatcherUnhandledException;

            // 创建命名互斥体，确保单实例运行
            bool createdNew;
            _appMutex = new Mutex(true, MutexName, out createdNew);

            if (!createdNew)
            {
                ShowMessageAndExit("程序已在运行中，请勿重复启动。");
                return;
            }
            this.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            base.OnStartup(e);
            ExitEvt += RequestExit;
            AppDomain.CurrentDomain.ProcessExit += (s, args) =>
            {
                ReleaseAppMutexSafely();
            };

            AppBootstrapper.GetInstance().OnStartUp();
        }

        /// <summary>
        /// 全局未捕获异常处理
        /// </summary>
        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            string errorMessage = $"发生异常：{e.Exception.Message}\n堆栈跟踪：{e.Exception.StackTrace}";
            Logger.Error($"发生未处理异常:{errorMessage}");

            MessageBox.Show($"发生未处理异常：{e.Exception.Message}", "程序错误", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true; // 阻止程序崩溃
        }

        /// <summary>
        /// 弹窗提示并退出程序
        /// </summary>
        private void ShowMessageAndExit(string message)
        {
            MessageBox.Show(message, "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            ShutdownGracefully();
        }

        /// <summary>
        /// 优雅退出程序
        /// </summary>
        private void ShutdownGracefully()
        {
            RequestExit();
        }

        /// <summary>
        /// 应用退出时释放资源
        /// </summary>
        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);

            // 释放互斥体
            ReleaseAppMutexSafely();

            // 可选：写退出日志
            LogExitTime();
        }

        /// <summary>
        /// 写退出日志（可选）
        /// </summary>
        private void LogExitTime()
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "exit.log");
                File.AppendAllText(path, $"退出时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");
            }
            catch
            {
                // 忽略日志写入失败
            }
        }

        private static void ReleaseAppMutexSafely()
        {
            if (_mutexReleased)
            {
                return;
            }

            try
            {
                _appMutex?.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // 当前线程未持有互斥体时会抛出，退出阶段可安全忽略。
            }
            finally
            {
                _appMutex?.Dispose();
                _appMutex = null;
                _mutexReleased = true;

            }
        }

        private static void RequestExit()
        {
            if (_isExiting)
            {
                return;
            }

            _isExiting = true;

            try
            {
                Current?.Shutdown();
            }
            catch
            {
                // 如果 WPF 已不可用，继续强制退出。
            }
            finally
            {
                Environment.Exit(0);
            }
        }
    }
 }
