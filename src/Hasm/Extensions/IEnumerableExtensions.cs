public static class IEnumerableExtensions
{
    public static bool ContainsSameElements<T>(this IEnumerable<T>? list, IEnumerable<T>? otherList)
    {
        if ((list?.Count() ?? 0) != (otherList?.Count() ?? 0)) { return false; }
        if ((list?.Count() ?? 0) == 0 && (otherList?.Count() ?? 0) == 0) { return true; }
        return list?.Except(otherList ?? []).Any() != true && otherList?.Except(list ?? []).Any() != true;
    }
}
