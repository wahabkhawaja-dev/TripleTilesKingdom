using System.Collections.Generic;

namespace Core.Services
{
    /// <summary>
    /// Placeholder IAnalyticsService. No analytics vendor has been chosen yet; this
    /// keeps every future LogEvent call site compiling and harmless until a real
    /// implementation is wired in GameRoot.
    /// </summary>
    public sealed class NoOpAnalyticsService : IAnalyticsService
    {
        public void LogEvent(string eventName, IReadOnlyDictionary<string, object> parameters = null)
        {
        }
    }
}
