
namespace FrameWork.Common
{
    public class TextExtension
    {
        public static string LimitMinAndMax(string text,int min = 0, int max = int.MaxValue)
        {
            string newText = text;
            if(int.TryParse(text, out int number))
            {
                if (number < min)
                {
                    newText = $"{min}";
                }
                else if (number > max) { 
                    newText = $"{max}";
                }
            }
            return newText;
        }
    }
}
