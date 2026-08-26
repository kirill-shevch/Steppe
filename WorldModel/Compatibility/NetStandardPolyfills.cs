#if NETSTANDARD2_1
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit
    {
    }

    [AttributeUsage(AttributeTargets.All, Inherited = false)]
    internal sealed class RequiredMemberAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.All, Inherited = false)]
    internal sealed class CompilerFeatureRequiredAttribute : Attribute
    {
        public CompilerFeatureRequiredAttribute(string featureName)
        {
            FeatureName = featureName;
        }

        public string FeatureName { get; }

        public bool IsOptional { get; init; }
    }
}

namespace System.Diagnostics.CodeAnalysis
{
    [AttributeUsage(AttributeTargets.Constructor, Inherited = false)]
    internal sealed class SetsRequiredMembersAttribute : Attribute
    {
    }
}
#endif

internal static class RuntimeCompatibility
{
    public static TEnum[] GetEnumValues<TEnum>() where TEnum : struct, Enum =>
        (TEnum[])Enum.GetValues(typeof(TEnum));

    public static void ThrowIfNull(object? value, string parameterName)
    {
        if (value is null)
        {
            throw new ArgumentNullException(parameterName);
        }
    }

    public static void ThrowIfNullOrWhiteSpace(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", parameterName);
        }
    }

    public static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    public static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

    public static void Clear<T>(T[] values) => Array.Clear(values, 0, values.Length);

    public sealed class MinPriorityQueue<TElement>
    {
        private TElement[] elements = new TElement[16];
        private float[] priorities = new float[16];

        public int Count { get; private set; }

        public void Enqueue(TElement element, float priority)
        {
            EnsureCapacity();
            var index = Count++;
            while (index > 0)
            {
                var parent = (index - 1) / 2;
                if (priorities[parent] <= priority)
                {
                    break;
                }

                elements[index] = elements[parent];
                priorities[index] = priorities[parent];
                index = parent;
            }

            elements[index] = element;
            priorities[index] = priority;
        }

        public bool TryDequeue(out TElement element, out float priority)
        {
            if (Count == 0)
            {
                element = default!;
                priority = default;
                return false;
            }

            element = elements[0];
            priority = priorities[0];
            Count--;
            if (Count == 0)
            {
                elements[0] = default!;
                return true;
            }

            var replacement = elements[Count];
            var replacementPriority = priorities[Count];
            elements[Count] = default!;
            var index = 0;
            while (true)
            {
                var left = index * 2 + 1;
                if (left >= Count)
                {
                    break;
                }

                var right = left + 1;
                var child = right < Count && priorities[right] < priorities[left] ? right : left;
                if (replacementPriority <= priorities[child])
                {
                    break;
                }

                elements[index] = elements[child];
                priorities[index] = priorities[child];
                index = child;
            }

            elements[index] = replacement;
            priorities[index] = replacementPriority;
            return true;
        }

        private void EnsureCapacity()
        {
            if (Count < elements.Length)
            {
                return;
            }

            Array.Resize(ref elements, elements.Length * 2);
            Array.Resize(ref priorities, priorities.Length * 2);
        }
    }
}
