using LPS.Core.Debug;

namespace LPS.Core.Ipc
{
    // Every thread work int a isolate sandbox
    public class SandBox
    {
        private Thread? thread_;

        public int ThreadId => thread_?.ManagedThreadId ?? throw new Exception("SandBox is empty.");

        private object? action_;

        private bool isAsyncAction_;

        private SandBox() { }

        public static SandBox Create(Action action)
        {
            var sandbox = new SandBox
            {
                action_ = action,
                isAsyncAction_ = false,
            };

            return sandbox;
        }

        public static SandBox Create(Func<Task> action)
        {
            var sandbox = new SandBox
            {
                action_ = action,
                isAsyncAction_ = true,
            };

            return sandbox;
        }

        public void Run()
        {
            thread_ = new Thread(() =>
            {
                try
                {
                    if (isAsyncAction_)
                    {
                        var promise = (action_ as Func<Task>)!();
                        promise.Wait();
                    }
                    else
                    {
                        (action_ as Action)!();
                    }
                }
                catch (ThreadInterruptedException ex) 
                {
                    Logger.Error(ex, "Sandbox thread interupted.");
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"Exception happend in SandBox");
                }
            });
            thread_.Start();
        }

        public void WaitForExit()
        {
            thread_!.Join();
        }

        public void Interrupt()
        {
            thread_!.Interrupt();
        }
    }
}
