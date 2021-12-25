using System;
using System.Collections.Generic;

namespace LPS.Core.IPC
{
    public class Dispatcher
    {
        public static readonly Dispatcher Default = new();

        public Dictionary<object, List<Action<object[]>>> callbacks_ = new();

        public void Register(IComparable key, Action<object[]> callback)
        {
            if (callbacks_.ContainsKey(key))
            {
                callbacks_[key].Add(callback);
            }
            else
            {
                callbacks_[key] = new() { callback };
            }
        }

        public void Unregiser(IComparable key, Action<object[]> callback)
        {
            if (callbacks_.ContainsKey(key))
            {
                var callbackList = callbacks_[key];
                callbackList.Remove(callback);
                if (callbackList.Count == 0)
                {
                    callbacks_.Remove(key);
                }
            }
        }

        public void Dispatch(IComparable key, params object[] args)
        {
            if (callbacks_.ContainsKey(key))
            {
                callbacks_[key].ForEach(cb => cb.Invoke(args));
            }
        }
    }
}
