using FrameWork.Common;
using FrameWork.Log;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace EEGTool.Models.Template
{
    public class TemplateFileManager : Singleton<TemplateFileManager>
    {

        private static string _templateFileDirectory = Path.Combine(Constants.LocalDataPath, "Templates");
        public List<TemplateModel> AllTemplates { get; set; } = new List<TemplateModel>();

        public void Init()
        {
            if (!Directory.Exists(_templateFileDirectory))
            {
                Directory.CreateDirectory(_templateFileDirectory);
                return;
            }

            ReadAllTemplates();
        }

        public void ReadAllTemplates()
        {
            if (!Directory.Exists(_templateFileDirectory))
                return;
            AllTemplates.Clear();
            var jsonFiles = Directory.GetFiles(_templateFileDirectory, "*_template.json");

            foreach (var file in jsonFiles)
            {
                try
                {
                    string jsonText = File.ReadAllText(file);

                    var template = JsonSerializer.Deserialize<TemplateModel>(jsonText, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        ReadCommentHandling = JsonCommentHandling.Skip,
                        AllowTrailingCommas = true
                    });

                    if (template != null)
                    {
                        AllTemplates.Add(template);
                        Logger.Info($"解析模板成功: {template.Name} ({Path.GetFileName(file)})");
                    }
                    else
                    {
                        Logger.Info($"模板为空: {Path.GetFileName(file)}");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Info($"解析模板失败: {Path.GetFileName(file)} - {ex.Message}");
                }
            }

            AllTemplates = AllTemplates.OrderBy(t =>
            {
                var match = Regex.Match(t.TemplateId, @"\d+");
                return match.Success ? long.Parse(match.Value) : long.MinValue;
            }).ToList();
        }

        public string SaveTemplate(TemplateModel template)
        {
            try
            {
                //保存之前，先判断是不是已经存在的模板，如果已经存在的模板，就先删除再保存
                if (!string.IsNullOrEmpty(template.TemplateId))
                {
                    DeleteTemplate(template.TemplateId);
                }

                if (!Directory.Exists(_templateFileDirectory))
                    Directory.CreateDirectory(_templateFileDirectory);

                // 自动生成模板 ID（包含时间）
                string timeStamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                template.TemplateId = $"Template_{timeStamp}";


                // 生成保存文件名（例如：Template_20251027_142530_template.json）
                string fileName = $"{template.TemplateId}_template.json";
                string filePath = Path.Combine(_templateFileDirectory, fileName);

                // JSON 序列化
                string json = JsonSerializer.Serialize(template, new JsonSerializerOptions
                {
                    WriteIndented = true, // 美化格式
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });

                // 保存文件
                File.WriteAllText(filePath, json);

                // 添加到内存缓存列表
                AllTemplates.Add(template);
                return template.TemplateId;
            }
            catch (Exception ex)
            {
                Logger.Info($"保存模板失败：{ex.Message}");
                return string.Empty;
            }
        }


        public bool DeleteTemplate(string templateId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(templateId))
                {
                    return false;
                }

                if (!Directory.Exists(_templateFileDirectory))
                {
                    return false;
                }

                // 查找文件：文件名以 TemplateId 开头且以 "_template.json" 结尾
                var targetFiles = Directory.GetFiles(_templateFileDirectory, $"{templateId}_template.json");

                if (targetFiles.Length == 0)
                {
                    Logger.Info($"未找到模板文件：{templateId}");
                    return false;
                }

                foreach (var file in targetFiles)
                {
                    File.Delete(file);
                }

                // 同步移除内存中的模板对象
                var removeItem = AllTemplates.FirstOrDefault(t => t.TemplateId == templateId);
                if (removeItem != null)
                {
                    AllTemplates.Remove(removeItem);
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.Info($"删除模板失败：{ex.Message}");
                return false;
            }
        }

        public bool UpdateTemplate(TemplateModel template)
        {
            try
            {
                if (template == null || string.IsNullOrWhiteSpace(template.TemplateId))
                {
                    return false;
                }

                if (!Directory.Exists(_templateFileDirectory))
                {
                    Directory.CreateDirectory(_templateFileDirectory);
                }

                string fileName = $"{template.TemplateId}_template.json";
                string filePath = Path.Combine(_templateFileDirectory, fileName);

                string json = JsonSerializer.Serialize(template, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });

                File.WriteAllText(filePath, json);

                var existingTemplate = AllTemplates.FirstOrDefault(t => t.TemplateId == template.TemplateId);
                if (existingTemplate != null)
                {
                    existingTemplate.Name = template.Name;
                    existingTemplate.Time = template.Time;
                    existingTemplate.EleDirectory = new ObservableCollection<Electrode>(template.EleDirectory);
                }
                else
                {
                    AllTemplates.Add(template);
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.Info($"更新模板失败：{ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取当前模板中通道的编号列表
        /// </summary>
        /// <returns></returns>
        public List<int> GetCurrentChannelList(TemplateModel templateModel)
        {
            List<int> channelList = new List<int>();

            // 校验当前模板和电极列表是否为空
            if (templateModel == null || templateModel.EleDirectory == null || !templateModel.EleDirectory.Any())
            {
                Logger.Info("当前模板为空或无电极配置，返回空通道列表");
                return channelList;
            }

            // 正则匹配 "Ch+数字" 格式（忽略大小写），捕获数字部分
            Regex chRegex = new Regex(@"^Ch(\d+)$", RegexOptions.IgnoreCase);

            foreach (var electrode in templateModel.EleDirectory)
            {
                int channelNum = 0; // 格式异常时默认值
                if (!string.IsNullOrWhiteSpace(electrode.Channel))
                {
                    Match match = chRegex.Match(electrode.Channel);
                    // 匹配成功则转换为数字
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int num))
                    {
                        channelNum = num;
                    }
                    else
                    {
                        Logger.Info($"电极[{electrode.Name}]的通道格式错误：{electrode.Channel}，默认赋值0");
                    }
                }
                else
                {
                    Logger.Info($"电极[{electrode.Name}]的通道为空，默认赋值0");
                }

                channelList.Add(channelNum);
            }

            return channelList;
        }

        /// <summary>
        /// 获取通道的点位名称
        /// </summary>
        /// <param name="ch"></param>
        /// <returns></returns>
        public string GetChannelName(string ch, TemplateModel templateModel)
        {
            // 入参空值校验
            if (string.IsNullOrWhiteSpace(ch))
            {
                Logger.Info("通道名称入参为空，返回空字符串");
                return string.Empty;
            }

            // 校验当前模板和电极列表是否为空
            if (templateModel == null || templateModel.EleDirectory == null || !templateModel.EleDirectory.Any())
            {
                Logger.Info("当前模板为空或无电极配置，无法获取通道对应点位名称");
                return string.Empty;
            }

            // 统一格式（转大写，去除空格），兼容 ch1、Ch1、CH1 等输入
            string targetCh = ch.Trim().ToUpper();

            // 遍历电极列表，匹配通道（忽略大小写）
            var electrode = templateModel.EleDirectory.FirstOrDefault(ele =>
                !string.IsNullOrWhiteSpace(ele.Channel) && ele.Channel.Trim().ToUpper() == targetCh);

            // 找到则返回点位名称，未找到返回空字符串
            string result = electrode?.Name ?? string.Empty;
            if (string.IsNullOrEmpty(result))
            {
                Logger.Info($"未找到通道[{ch}]对应的点位名称");
            }

            return result;
        }
    }
}
