using System.Reflection;

namespace FrameWork.Tools
{
    public static  class ConvertTool
    {
        public static Dictionary<string, object> ToDictionary(this object obj)
        {
            if (obj == null) return new Dictionary<string, object>();

            Dictionary<string, object> dict = new Dictionary<string, object>();
            PropertyInfo[] properties = obj.GetType().GetProperties(
                BindingFlags.Public | BindingFlags.Instance
            );

            foreach (PropertyInfo prop in properties)
            {
                if (prop.CanRead)
                {
                    object value = prop.GetValue(obj);
                    dict.Add(prop.Name, value);
                }
            }

            return dict;
        }
    }
}
