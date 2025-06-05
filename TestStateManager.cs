using System;

namespace friction_tester
{
    public static class TestStateManager
    {
        public static event Action<bool> OnTestActivityChanged;

        private static int _activeTestCount = 0;
        private static readonly object _lock = new object();

        public static bool IsTestInProgress => _activeTestCount > 0;

        public static void NotifyTestStarted()
        {
            lock (_lock)
            {
                _activeTestCount++;
                if (_activeTestCount == 1) // Only raise event when the first test starts
                {
                    OnTestActivityChanged?.Invoke(true);
                    Logger.Log("[TestStateManager] A test has started. Active tests: " + _activeTestCount);
                }
            }
        }

        public static void NotifyTestCompleted()
        {
            lock (_lock)
            {
                if (_activeTestCount > 0)
                {
                    _activeTestCount--;
                    if (_activeTestCount == 0) // Only raise event when the last test completes
                    {
                        OnTestActivityChanged?.Invoke(false);
                        Logger.Log("[TestStateManager] All tests completed. Active tests: " + _activeTestCount);
                    }
                }
            }
        }
    }
} 