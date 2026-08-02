namespace Presentation.Pooling
{
    public interface IObjectPool<T> where T : class
    {
        /// <summary>Eagerly constructs and deactivates <paramref name="count"/> instances up front.</summary>
        void Prewarm(int count);

        /// <summary>Returns an active instance, reusing an inactive one if available.</summary>
        T Get();

        /// <summary>Deactivates and returns an instance to the pool for reuse.</summary>
        void Release(T item);

        int CountInactive { get; }
        int CountActive { get; }
    }
}
