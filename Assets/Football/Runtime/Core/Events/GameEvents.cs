using System;
using System.Collections.Generic;

namespace Football.Core
{
    public static class GameEvents
    {
        private static readonly Dictionary<Type, List<Delegate>> _handlers = new();

        public static void Subscribe<T>(Action<T> handler) where T : struct, IGameEvent
        {
            var type = typeof(T);
            if (!_handlers.ContainsKey(type))
                _handlers[type] = new List<Delegate>();
            _handlers[type].Add(handler);
        }

        public static void Unsubscribe<T>(Action<T> handler) where T : struct, IGameEvent
        {
            var type = typeof(T);
            if (_handlers.ContainsKey(type))
                _handlers[type].Remove(handler);
        }

        public static void Raise<T>(T gameEvent) where T : struct, IGameEvent
        {
            var type = typeof(T);
            if (!_handlers.ContainsKey(type)) return;

            for (int i = _handlers[type].Count - 1; i >= 0; i--)
            {
                if (_handlers[type][i] is Action<T> handler)
                    handler.Invoke(gameEvent);
            }
        }

        public static void Clear()
        {
            _handlers.Clear();
        }
    }
}
