
using Flurl;
using Flurl.Http;
using FrameWork.Common;
using FrameWork.Log;
using Newtonsoft.Json;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using FrameWork.UserControls.ToastViewControl;
using JsonSerializer = System.Text.Json.JsonSerializer;


namespace FrameWork.NetWork
{
    public class ApiResponse
    {
        public int Code { get; set; }
        public Object Data { get; set; } = new Object();
        public string? Msg { get; set; } = "";
    }

    public static class HttpHelper
    {
        private static string BaseUrl = "http://192.168.1.115:18081";
        private static Dictionary<string,int> RetryCount = new Dictionary<string, int>();
        // TODO:caojian 重试流程(目前业务不需要屏蔽)
        private static int RetryMaxCount = 0;                                            //重试最大次数
        private static readonly bool _isUseProxy = false;
        private static readonly IFlurlClient _defaultClient = new FlurlClient();         // 不走代理

        public static void Init(string ip,int port)
        {
            BaseUrl = $"http://{ip}:{port}";
            HttpParameter.HostAddress = ip;
            HttpParameter.Port = $"{port}";
        }

        private static readonly IFlurlClient _proxyClient = new FlurlClient(new HttpClient(new HttpClientHandler
        {
            Proxy = new WebProxy("192.168.0.0.1"),
            UseProxy = true,
            ServerCertificateCustomValidationCallback = (msg, cert, chain, errors) => true // 仅调试用
        }));
      

        private static string BuildUrl(string path)
        {
            return BaseUrl.AppendPathSegment(path).ToString();
        }

        /// <summary>
        /// 发起GET请求，支持附加查询参数
        /// </summary>
        /// <typeparam name="T">接受的数据Model</typeparam>
        /// <param name="path">请求路由</param>
        /// <param name="queryParams">查询参数</param>
        /// <returns></returns>
        public static async Task<(bool,T?)> GetAsyn<T>(string path,Dictionary<string,object>? queryParams = null)
        {
            Dictionary<string, string>? headers = new Dictionary<string, string>()
            {
                { "Authorization", HttpParameter.Authorization }
            };
            var client = _isUseProxy ? _proxyClient : _defaultClient;

            try
            {
                var request = client.Request(BuildUrl(path))
                                    .SetQueryParams(queryParams)
                                    .WithHeaders(headers)
                                    .WithTimeout(30);

                var response = await request.GetStringAsync();

                var result = JsonConvert.DeserializeObject<ApiResponse>(response);

                var isSuccess = result?.Code == 0 || result.Code == 200;

                if (isSuccess) // 请求成功
                {
                    if (RetryCount.ContainsKey(path))
                        RetryCount.Remove(path);

                    var data = JsonConvert.DeserializeObject<T>(result.Data?.ToString() ?? "");
                    return (isSuccess ,data);
                }

                if (result?.Code == 500)
                {
                    await HandleErrorCode(result.Code, "服务器异常,请联系管理员~");
                    return (false,default);
                }

                if (!isSuccess)
                {
                    await HandleErrorCode(result.Code, result.Msg);
                    return (isSuccess,default);
                }
            }
            catch (FlurlHttpException ex)
            {
                var statusCode = ex.Call.Response?.StatusCode;

                // 特殊处理 400，不抛出
                if (statusCode == 500)
                {
                    var responseStr = await ex.GetResponseStringAsync();
                    var result = JsonConvert.DeserializeObject<ApiResponse>(responseStr);
                    await HandleErrorCode(result?.Code, result?.Msg);
                    return (false, default);
                }

                Logger.Info("[HttpHelper-GetAsyn] 请求异常：" + ex.Message);
                var error = ex.ToString();
                if (ex.InnerException != null)
                {
                    error = ex.InnerException.Message;
                    ToastManager.Show(error);
                    return (false, default);
                }
                if (RetryCount.ContainsKey(path) && RetryCount[path] == RetryMaxCount)
                {
                    ToastManager.Show(error);
                }
                return (false,default);
            }
            catch (Exception ex)
            {
                Logger.Info("[HttpHelper-GetAsyn] 未知异常：" + ex.Message);
                if (RetryCount.ContainsKey(path) && RetryCount[path] == RetryMaxCount)
                {
                    ToastManager.Show(ex.Message);
                }
                return (false, default);
            }

            // 重试逻辑
            if (RetryCount.ContainsKey(path) && RetryCount[path] >= RetryMaxCount)
            {
                Logger.Debug($"[HttpHelper-GetAsyn]:重试次数{RetryCount[path]}" + path);
                RetryCount.Remove(path);
                return (false, default);
            }
            else if (!RetryCount.ContainsKey(path))
            {
                RetryCount.Add(path, 1);
            }
            else
            {
                RetryCount[path]++;
            }

            // 重试请求
            return await GetAsyn<T>(path, queryParams);
        }

        /// <summary>
        /// 发起 POST 请求，发送 JSON 数据。
        /// </summary>
        public static async Task<(bool,T?)> PostAsync<T>(string path, Dictionary<string, object>? bodys = null)
        {
            // 本地递归函数，避免多处 return
            async Task<(bool,T?)> RetryAsync()
            {
                if (RetryCount.TryGetValue(path, out int count) && count >= RetryMaxCount)
                {
                    Logger.Debug($"[HttpHelper-PostAsync]:重试次数{count} 达到上限, 路径: {path}");
                    RetryCount.Remove(path);
                    return (false,default);
                }

                if (!RetryCount.ContainsKey(path))
                    RetryCount[path] = 0;
                else
                    RetryCount[path]++;

                return await PostAsync<T>(path, bodys);
            }

            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = false,
                };

                string bodyJson = JsonSerializer.Serialize(bodys, options);

                Dictionary<string, string> headers = new Dictionary<string, string>()
                {
                    { "content-type", "application/json; charset=UTF-8" },
                    {"Authorization", HttpParameter.Authorization}
                };

                var client = _isUseProxy ? _proxyClient : _defaultClient;

                var request = client.Request(BuildUrl(path))
                                    .WithHeaders(headers)
                                    .WithTimeout(30);


                string response = await (await request.PostStringAsync(bodyJson)).GetStringAsync();

                var result = JsonConvert.DeserializeObject<ApiResponse>(response);
                var isSuccess = result?.Code == 0 || result.Code == 200;

                if (isSuccess)
                {
                    RetryCount.Remove(path);

                    if (result.Data == null)
                        return default;

                    var value = result.Data;
                    string dataStr = value switch
                    {
                        null => null,
                        bool b => b.ToString(),                       // 布尔
                        System.Text.Json.JsonElement e => e.GetRawText(), // JSON 元素
                        _ => value.ToString()                         // 其他类型
                    };

                    if (string.IsNullOrWhiteSpace(dataStr))
                        return default;

                    T data;
                    if (typeof(T) == typeof(object))
                    {
                        // 不指定类型反序列化
                        var temp = value;
                        data = (T)temp;
                    }
                    else
                    {
                        data = JsonConvert.DeserializeObject<T>(dataStr);
                    }
                    return (isSuccess,data);
                }

                if (result?.Code == 500)
                {
                    await HandleErrorCode(result.Code, "服务器异常, 请联系管理员~");
                    return default;
                }

                if (!isSuccess)
                {
                    await HandleErrorCode(result.Code, result.Msg);
                    return default;
                }

                return await RetryAsync();
            }
            catch (FlurlHttpException ex)
            {
                var statusCode = ex.Call.Response?.StatusCode;

                if (statusCode == 500)
                {
                    var responseStr = await ex.GetResponseStringAsync();
                    var result = JsonConvert.DeserializeObject<ApiResponse>(responseStr);
                    await HandleErrorCode(result?.Code, result?.Msg);
                    return default;
                }

                string error = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                Logger.Debug($"[HttpHelper-PostAsync] 请求异常: {error}");

                if (RetryCount.ContainsKey(path) && RetryCount[path] == RetryMaxCount)
                {
                    ToastManager.Show(error);
                }
                return await RetryAsync();
            }
            catch (Exception ex)
            {
                Logger.Debug("[HttpHelper-PostAsync] 未知异常：" + ex.Message);
                if (RetryCount.ContainsKey(path) && RetryCount[path] == RetryMaxCount)
                {
                    ToastManager.Show(ex.Message);
                }
                return (false,default);
            }

            return (false, default);
        }

        public static async Task<(bool, T?)> DeleteAsync<T>(string path, Dictionary<string, object>? queryParams = null)
        {
            async Task<(bool, T?)> RetryAsync()
            {
                if (RetryCount.TryGetValue(path, out int count) && count >= RetryMaxCount)
                {
                    Logger.Debug($"[HttpHelper-DeleteAsync]:重试次数{count} 达到上限, 路径: {path}");
                    RetryCount.Remove(path);
                    return (false, default);
                }

                if (!RetryCount.ContainsKey(path))
                    RetryCount[path] = 0;
                else
                    RetryCount[path]++;

                return await DeleteAsync<T>(path, queryParams);
            }

            try
            {
                Dictionary<string, string> headers = new Dictionary<string, string>()
        {
            { "Authorization", HttpParameter.Authorization }
        };

                var client = _isUseProxy ? _proxyClient : _defaultClient;

                var request = client.Request(BuildUrl(path))
                                    .SetQueryParams(queryParams)
                                    .WithHeaders(headers)
                                    .WithTimeout(30);

                var response = await request.DeleteAsync();
                var responseStr = await response.GetStringAsync();

                var result = JsonConvert.DeserializeObject<ApiResponse>(responseStr);
                var isSuccess = result?.Code == 0 || result.Code == 200;

                if (isSuccess)
                {
                    RetryCount.Remove(path);

                    if (result.Data == null)
                        return (true, default);

                    var value = result.Data;
                    string dataStr = value switch
                    {
                        null => null,
                        bool b => b.ToString(),
                        System.Text.Json.JsonElement e => e.GetRawText(),
                        _ => value.ToString()
                    };

                    if (string.IsNullOrWhiteSpace(dataStr))
                        return (true, default);

                    T data;
                    if (typeof(T) == typeof(object))
                    {
                        data = (T)value;
                    }
                    else
                    {
                        data = JsonConvert.DeserializeObject<T>(dataStr);
                    }

                    return (true, data);
                }

                if (result?.Code == 500)
                {
                    await HandleErrorCode(result.Code, "服务器异常, 请联系管理员~");
                    return (false, default);
                }

                if (!isSuccess)
                {
                    await HandleErrorCode(result.Code, result.Msg);
                    return (false, default);
                }

                return await RetryAsync();
            }
            catch (FlurlHttpException ex)
            {
                var statusCode = ex.Call.Response?.StatusCode;

                if (statusCode == 500)
                {
                    var responseStr = await ex.GetResponseStringAsync();
                    var result = JsonConvert.DeserializeObject<ApiResponse>(responseStr);
                    await HandleErrorCode(result?.Code, result?.Msg);
                    return (false, default);
                }

                string error = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                Logger.Debug($"[HttpHelper-DeleteAsync] 请求异常: {error}");

                if (RetryCount.ContainsKey(path) && RetryCount[path] == RetryMaxCount)
                {
                    ToastManager.Show(error);
                }

                return await RetryAsync();
            }
            catch (Exception ex)
            {
                Logger.Debug("[HttpHelper-DeleteAsync] 未知异常：" + ex.Message);
                if (RetryCount.ContainsKey(path) && RetryCount[path] == RetryMaxCount)
                {
                    ToastManager.Show(ex.Message);
                }
                return (false, default);
            }
        }

        public static async Task<(bool, T?)> PutAsync<T>(string path, Dictionary<string, object>? bodys = null)
        {
            async Task<(bool, T?)> RetryAsync()
            {
                if (RetryCount.TryGetValue(path, out int count) && count >= RetryMaxCount)
                {
                    Logger.Debug($"[HttpHelper-PutAsync]:重试次数{count} 达到上限, 路径: {path}");
                    RetryCount.Remove(path);
                    return (false, default);
                }

                if (!RetryCount.ContainsKey(path))
                    RetryCount[path] = 0;
                else
                    RetryCount[path]++;

                return await PutAsync<T>(path, bodys);
            }

            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = false,
                };

                string bodyJson = JsonSerializer.Serialize(bodys, options);

                Dictionary<string, string> headers = new Dictionary<string, string>()
                {
                    { "content-type", "application/json; charset=UTF-8" },
                    { "Authorization", HttpParameter.Authorization }
                };

                var client = _isUseProxy ? _proxyClient : _defaultClient;

                var request = client.Request(BuildUrl(path))
                                    .WithHeaders(headers)
                                    .WithTimeout(30);

                string response = await (await request.PutStringAsync(bodyJson)).GetStringAsync();

                var result = JsonConvert.DeserializeObject<ApiResponse>(response);
                var isSuccess = result?.Code == 0 || result.Code == 200;

                if (isSuccess)
                {
                    RetryCount.Remove(path);

                    if (result.Data == null)
                        return default;

                    var value = result.Data;
                    string dataStr = value switch
                    {
                        null => null,
                        bool b => b.ToString(),
                        System.Text.Json.JsonElement e => e.GetRawText(),
                        _ => value.ToString()
                    };

                    if (string.IsNullOrWhiteSpace(dataStr))
                        return default;

                    T data;
                    if (typeof(T) == typeof(object))
                    {
                        data = (T)value;
                    }
                    else
                    {
                        data = JsonConvert.DeserializeObject<T>(dataStr);
                    }
                    return (isSuccess, data);
                }

                if (result?.Code == 500)
                {
                    await HandleErrorCode(result.Code, "服务器异常, 请联系管理员~");
                    return default;
                }

                if (!isSuccess)
                {
                    await HandleErrorCode(result.Code, result.Msg);
                    return default;
                }

                return await RetryAsync();
            }
            catch (FlurlHttpException ex)
            {
                var statusCode = ex.Call.Response?.StatusCode;

                if (statusCode == 500)
                {
                    var responseStr = await ex.GetResponseStringAsync();
                    var result = JsonConvert.DeserializeObject<ApiResponse>(responseStr);
                    await HandleErrorCode(result?.Code, result?.Msg);
                    return default;
                }

                string error = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                Logger.Debug($"[HttpHelper-PutAsync] 请求异常: {error}");

                if (RetryCount.ContainsKey(path) && RetryCount[path] == RetryMaxCount)
                {
                    ToastManager.Show(error);
                }

                return await RetryAsync();
            }
            catch (Exception ex)
            {
                Logger.Debug("[HttpHelper-PutAsync] 未知异常：" + ex.Message);
                if (RetryCount.ContainsKey(path) && RetryCount[path] == RetryMaxCount)
                {
                    ToastManager.Show(ex.Message);
                }
                return (false, default);
            }
        }


        #region  下载文件

        /// <summary>
        /// 下载任意类型文件并保存到本地指定文件夹（重名文件自动替换）
        /// </summary>
        /// <param name="url">接口路径</param>
        /// <param name="saveDir">本地保存文件夹（变量传入）</param>
        /// <param name="queryParams">查询参数</param>
        /// <returns>下载结果：(是否成功, 本地保存路径)</returns>
        public static async Task<(bool, Stream)> DownloadFileToLocalAsync(
            string url,
            Dictionary<string, object>? queryParams = null)
        {
           
            // 保留原有授权头
            Dictionary<string, string> headers = new(){{ "Authorization", HttpParameter.Authorization }};
            var client = _isUseProxy ? _proxyClient : _defaultClient;
            
            try
            {
                var request = client.Request(BuildUrl(url))
                                    .SetQueryParams(queryParams)
                                    .WithHeaders(headers)
                                    .WithTimeout(60); // 延长超时到60秒（适配大文件）

                // 1. 执行请求，获取 Flurl 响应对象
                var response = await request.GetAsync();

                // 移除扩展方法，直接判断状态码
                int statusCode = response.StatusCode;
                if (statusCode != 200 )
                {
                    string errorMsg = "[HttpHelper-DownloadFileToLocalAsync] 接口URL不能为空";
                    Logger.Error(errorMsg);
                    ToastManager.Show("接口地址无效");
                     return (false, null);
                }

                // 5. 获取响应流（返回给调用方，由调用方处理文件写入）
                Stream responseStream = await response.GetStreamAsync();
               
                // 下载成功：清除重试计数
                if (RetryCount.ContainsKey(url))
                    RetryCount.Remove(url);

                return (true, responseStream);
            }
            catch (FlurlHttpException ex)
            {
                // 7. 处理网络相关异常
                int statusCode = ex.StatusCode ?? -1;
                string errorMsg = $"[HttpHelper-DownloadFileToLocalAsync] Flurl请求异常：路径={url}，状态码={statusCode}，消息={ex.Message}，详情={ex.ToString()}";
                Logger.Error(errorMsg);

                string toastMsg = statusCode switch
                {
                    (int)HttpStatusCode.InternalServerError => "服务器异常，无法下载文件",
                    (int)HttpStatusCode.NotFound => "请求的文件不存在",
                    (int)HttpStatusCode.BadRequest => "请求参数错误，无法下载",
                    (int)HttpStatusCode.Unauthorized => "未授权访问，无法下载",
                    _ => $"文件下载失败：{ex.Message}"
                };

                if (RetryCount.ContainsKey(url) && RetryCount[url] == RetryMaxCount)
                    ToastManager.Show(toastMsg);

                // 仅5xx/网络异常重试
                if (statusCode >= 500 || statusCode == -1)
                   
                return (false, null);
            }
            catch (Exception ex)
            {
                // 8. 处理其他未知异常
                string errorMsg = $"[HttpHelper-DownloadFileToLocalAsync] 未知异常：路径={url}，消息={ex.Message}，详情={ex.ToString()}";
                Logger.Error(errorMsg);

                if (RetryCount.ContainsKey(url) && RetryCount[url] == RetryMaxCount)
                    ToastManager.Show("文件下载未知异常，请稍后重试");

                return (false, null);
            }
            return (false, null);
        }

        #endregion


        private static async Task HandleErrorCode(int? code,string? content)
        {
            if (code == 401)
            {
                return;
            }
            ToastManager.Show(content ?? "");
            await Task.Delay(200);
        }
    }
}
