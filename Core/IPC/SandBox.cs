using System.Threading;
using System;

namespace LPS.Core.IPC
{
    // Every thread work int a isolate sandbox
    public class SandBox
    {
        private Thread m_thread = null;

        public int ThreadID => m_thread is null ? throw new Exception("SandBox is empty.") : m_thread.ManagedThreadId;

        static void Init()
        {
           // TODO: use threadpool
        }

        void Run(Action action)
        {
            m_thread = new Thread(() =>
            {
                try
                {
                    action();
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
            m_thread.Start();
        }

        void Interrupt()
        {
            m_thread.Interrupt();
        }
    }
}