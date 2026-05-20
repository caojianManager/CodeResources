
namespace FrameWork.Tools
{
 

    public static class DictionaryExtensions
    {
        /// <summary>
        /// 深拷贝 Dictionary&lt;string, T&gt;，支持 T 为 double[] 或 double[][]（任意嵌套数组）
        /// </summary>
        public static Dictionary<string, T> DeepCopyDictionary<T>(Dictionary<string, T> original)
        {
            if (original == null)
                return null;

            var copy = new Dictionary<string, T>(original.Count, original.Comparer);

            foreach (var kvp in original)
            {
                copy[kvp.Key] = DeepCopyValue(kvp.Value);
            }

            return copy;
        }

        // 根据类型递归拷贝
        private static T DeepCopyValue<T>(T value)
        {
            if (value == null)
                return default;

            // 如果是一维数组
            if (value is double[] arr1)
            {
                var copy1 = new double[arr1.Length];
                Array.Copy(arr1, copy1, arr1.Length);
                return (T)(object)copy1;
            }

            // 如果是二维数组 double[][]
            if (value is double[][] arr2)
            {
                var copy2 = new double[arr2.Length][];
                for (int i = 0; i < arr2.Length; i++)
                {
                    if (arr2[i] != null)
                    {
                        var rowCopy = new double[arr2[i].Length];
                        Array.Copy(arr2[i], rowCopy, arr2[i].Length);
                        copy2[i] = rowCopy;
                    }
                }
                return (T)(object)copy2;
            }

            // 如果是其他类型，尝试直接返回原值（值类型或不可处理类型）
            return value;
        }
    }

}
