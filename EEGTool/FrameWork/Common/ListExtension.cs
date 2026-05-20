
namespace FrameWork.Common
{
    public class ListExtension
    {
        public static bool IsUidListEqual<T>(List<T> listA, List<T> listB, Func<T, string> uidSelector)
        {
            var uidsA = listA.Select(uidSelector).OrderBy(x => x).ToList();
            var uidsB = listB.Select(uidSelector).OrderBy(x => x).ToList();
            return uidsA.SequenceEqual(uidsB);
        }
    }
}
