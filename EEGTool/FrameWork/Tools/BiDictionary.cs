
namespace FrameWork.Tools
{
    public class BiDictionary<TKey, TValue>
    {
        private readonly Dictionary<TKey, TValue> _forward = new();
        private readonly Dictionary<TValue, TKey> _reverse = new();

        public TValue this[TKey key]
        {
            get => _forward[key];
            set => Set(key, value);
        }

        public TKey this[TValue value, bool reverse]
        {
            get => _reverse[value];
        }

        public void Add(TKey key, TValue value)
        {
            if (_forward.ContainsKey(key))
                throw new ArgumentException($"Duplicate key: {key}");
            if (_reverse.ContainsKey(value))
                throw new ArgumentException($"Duplicate value: {value}");

            _forward[key] = value;
            _reverse[value] = key;
        }

        public void Clear()
        {
            _forward.Clear();
            _reverse.Clear();
        }

        public void Set(TKey key, TValue value)
        {
            // 如果已存在，先删除旧映射
            if (_forward.TryGetValue(key, out var oldValue))
                _reverse.Remove(oldValue);

            if (_reverse.TryGetValue(value, out var oldKey))
                _forward.Remove(oldKey);

            _forward[key] = value;
            _reverse[value] = key;
        }

        public bool TryGetByKey(TKey key, out TValue value) => _forward.TryGetValue(key, out value);
        public bool TryGetByValue(TValue value, out TKey key) => _reverse.TryGetValue(value, out key);

        public bool ContainsKey(TKey key) => _forward.ContainsKey(key);
        public bool ContainsValue(TValue value) => _reverse.ContainsKey(value);

        public IEnumerable<TKey> Keys => _forward.Keys;
        public IEnumerable<TValue> Values => _reverse.Keys;
    }

}
