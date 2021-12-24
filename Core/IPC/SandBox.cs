using System;
using System.Threading;

namespace LPS.Core.IPC
{
    // Every thread work int a isolate sandbox
    public class SandBox
    {
        private Thread thread_ = null;

        public int ThreadID => thread_ is null ? throw new Exception("SandBox is empty.") : thread_.ManagedThreadId;

        private Action action_;

        private SandBox() { }

        public static SandBox Create(Action action)
        {
            var sandbox = new SandBox
            {
                action_ = action
            };

            return sandbox;
        }

        public void Run()
        {
            thread_ = new Thread(() =>
            {
                try
                {
                    action_();
                }
                catch (ThreadInterruptedException) 
                {
                    Console.WriteLine("Sandbox thread interupted.");
                }
                catch (System.Exception ex)
                {
                    Console.WriteLine($"Exception happend in SandBox: {ex.Message}");
                }
            });
            thread_.Start();
        }

        public void WaitForExit()
        {
            thread_.Join();
        }

        public void Interrupt()
        {
            thread_.Interrupt();
        }
    }
}
