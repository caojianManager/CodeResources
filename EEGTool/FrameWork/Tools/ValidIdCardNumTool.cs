using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DrugControlApp.FrameWork.Tools
{
    public class ValidIdCardNumTool
    {
        /// <summary>
        /// 自动识别并验证证件号码
        /// </summary>
        public static bool ValidateIDNumberOutType(string id, out string idType)
        {
            idType = "未知";

            if (ValidateChineseID(id))
            {
                idType = "居民身份证";
                return true;
            }
            else if (ValidateForeignPermanentResidentID(id))
            {
                idType = "外国人永久居留身份证";
                return true;
            }
            else if (ValidateHKMacaoResidentID(id))
            {
                idType = id.StartsWith("H", StringComparison.OrdinalIgnoreCase) ?
                         "香港居民居住证" : "澳门居民居住证";
                return true;
            }
            else if (ValidatePassportNumber(id))
            {
                idType = "护照";
                return true;
            }

            return false;
        }
        public static bool ValidateIDNumber(string id)
        {

            if (ValidateChineseID(id))
            {
                return true;
            }
            else if (ValidateForeignPermanentResidentID(id))
            {
                return true;
            }
            else if (ValidateHKMacaoResidentID(id))
            {
                return true;
            }
            else if (ValidatePassportNumber(id))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// 验证护照号码(通用)
        /// </summary>
        public static bool ValidatePassportNumber(string passport)
        {
            if (string.IsNullOrWhiteSpace(passport)) return false;

            // 中国护照：E/K/D/S/P开头 + 7或8位数字
            // 外国护照：1-2位字母 + 5-8位数字/字母
            return Regex.IsMatch(passport, @"^(?![IO])[A-Za-z0-9]{6,9}$") ||
                   Regex.IsMatch(passport, @"^[EeKkDdSsPp]\d{7,8}$");
        }
        /// <summary>
        /// 验证港澳居民居住证号码
        /// </summary>
        public static bool ValidateHKMacaoResidentID(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;

            // 格式：H/M开头 + 8位数字 + (0-9A-Z)校验码
            return Regex.IsMatch(id, @"^[HMhm]\d{8}[0-9A-Za-z]$");
        }
        /// <summary>
        /// 验证外国人永久居留身份证号码
        /// </summary>
        public static bool ValidateForeignPermanentResidentID(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;

            // 格式：前3位为国籍代码，后15位为数字
            return Regex.IsMatch(id, @"^[A-Za-z]{3}\d{15}$");
        }
        /// <summary>
        /// 验证中国大陆身份证号码
        /// </summary>
        public static bool ValidateChineseID(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;

            // 18位身份证正则表达式
            if (Regex.IsMatch(id, @"^[1-9]\d{5}(18|19|20)\d{2}(0[1-9]|1[0-2])(0[1-9]|[12]\d|3[01])\d{3}[0-9Xx]$"))
            {
                // 校验码验证
                int[] weights = { 7, 9, 10, 5, 8, 4, 2, 1, 6, 3, 7, 9, 10, 5, 8, 4, 2 };
                char[] checkCodes = { '1', '0', 'X', '9', '8', '7', '6', '5', '4', '3', '2' };

                int sum = 0;
                for (int i = 0; i < 17; i++)
                {
                    sum += (id[i] - '0') * weights[i];
                }

                char checkCode = checkCodes[sum % 11];
                return char.ToUpper(id[17]) == checkCode;
            }

            // 15位身份证正则表达式(早期身份证)
            if (Regex.IsMatch(id, @"^[1-9]\d{5}\d{2}(0[1-9]|1[0-2])(0[1-9]|[12]\d|3[01])\d{3}$"))
            {
                return true;
            }

            return false;
        }
    }
}
