using System.Collections;
using System.Collections.Generic;

namespace RPG
{
    /// <summary>
    /// A read-only view of a list that <c>foreach</c> walks without allocating (the list's own struct enumerator), for
    /// registries the HUD reads every frame (<c>Health.Spawned</c>, <c>Vendor.All</c>...). It is still an
    /// <see cref="IReadOnlyList{T}"/> for code that wants one.
    /// </summary>
    public readonly struct ListView<T> : IReadOnlyList<T>
    {
        private readonly List<T> list;

        public ListView(List<T> list) => this.list = list;

        public int Count => list != null ? list.Count : 0;
        public T this[int index] => list[index];

        public List<T>.Enumerator GetEnumerator() => (list ?? Empty).GetEnumerator();
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private static readonly List<T> Empty = new List<T>();
    }
}
