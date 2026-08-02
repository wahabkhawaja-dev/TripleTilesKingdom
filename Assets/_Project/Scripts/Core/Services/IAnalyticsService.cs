using System.Collections.Generic;

namespace Core.Services
{
    /// <summary>
    /// Thin abstraction over whichever analytics SDK we eventually integrate. Kept
    /// generic (event name + parameter bag) so gameplay code never references a
    /// specific vendor SDK directly.
    /// </summary>
    public interface IAnalyticsService
    {
        void LogEvent(string eventName, IReadOnlyDictionary<string, object> parameters = null);
    }
}
