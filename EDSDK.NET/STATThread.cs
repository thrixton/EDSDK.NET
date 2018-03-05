using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace EDSDK.NET
{
    /// <summary>
    /// Helper class to create or run code on STA threads
    /// </summary>
    public static class STAThread
    {
        static ILogger logger;

        public static void SetLogAction(ILogger logger)
        {
            STAThread.logger = logger;
        }

        /// <summary>
        /// The object that is used to lock the live view thread
        /// </summary>
        public static readonly object ExecLock = new object();
        /// <summary>
        /// States if the calling thread is an STA thread or not
        /// </summary>
        public static bool IsSTAThread
        {
            get { return Thread.CurrentThread.GetApartmentState() == ApartmentState.STA; }
        }

        /// <summary>
        /// The main thread where everything will be executed on
        /// </summary>
        private static Thread main;
        /// <summary>
        /// The action to be executed
        /// </summary>
        private static Action runAction;
        /// <summary>
        /// Storage for an exception that might have happened on the execution thread
        /// </summary>
        private static Exception runException;
        /// <summary>
        /// States if the execution thread is currently running
        /// </summary>
        private static bool isRunning = false;
        /// <summary>
        /// Lock object to make sure only one command at a time is executed
        /// </summary>
        private static object runLock = new object();
        /// <summary>
        /// Lock object to synchronize between execution and calling thread
        /// </summary>
        private static object threadLock = new object();

        /// <summary>
        /// Starts the execution thread
        /// </summary>
        internal static void Init()
        {
            if (!isRunning)
            {
                main = Create(SafeExecutionLoop);
                isRunning = true;
                main.Start();
            }
        }

        /// <summary>
        /// Shuts down the execution thread
        /// </summary>
        internal static void Shutdown()
        {
            if (isRunning)
            {
                isRunning = false;
                bool locked = false;

                try
                {
                    Monitor.TryEnter(threadLock, TimeSpan.FromSeconds(30), ref locked);
                    if (locked)
                    {
                        Monitor.Pulse(threadLock);
                    }
                    else
                    {
                        logger?.LogError("Lock request timeout expired. LockObject: {LockObject}", nameof(threadLock));
                    }
                }
                finally
                {
                    if (locked)
                    {
                        Monitor.Exit(threadLock);
                    }
                }
                main.Join(TimeSpan.FromSeconds(30));
            }
        }

        /// <summary>
        /// Creates an STA thread that can safely execute SDK commands
        /// </summary>
        /// <param name="a">The command to run on this thread</param>
        /// <returns>An STA thread</returns>
        public static Thread Create(Action a)
        {
            return Create(a, "STA thread: " + Guid.NewGuid().ToString());
        }

        /// <summary>
        /// Creates an STA thread that can safely execute SDK commands
        /// </summary>
        /// <param name="a">The command to run on this thread</param>
        /// <param name="threadName">The name of this thread</param>
        /// <returns>An STA thread</returns>
        public static Thread Create(Action a, string threadName)
        {
            var thread = new Thread(new ThreadStart(a));
            thread.SetApartmentState(ApartmentState.STA);
            thread.Name = threadName;
            logger.LogInformation("Created STA Thread. ThreadName: {ThreadName}, ApartmentState: {ApartmentState}", thread.Name, thread.GetApartmentState());
            return thread;
        }

        public static void TryLockAndExecute(object lockObject, string lockObjectName, TimeSpan timeout, Action action)
        {
            bool locked = false;

            try
            {
                Monitor.TryEnter(lockObject, timeout, ref locked);
                if (locked)
                {
                    action();
                }
                else
                {
                    logger?.LogError("Lock request timeout expired. LockObject: {LockObject}", lockObjectName);
                }
            }
            finally
            {
                if (locked)
                {
                    Monitor.Exit(lockObject);
                }
            }
        }


        /// <summary>
        /// Safely executes an SDK command
        /// </summary>
        /// <param name="a">The SDK command</param>
        public static void ExecuteSafely(Action a)
        {
            TryLockAndExecute(runLock, nameof(runLock), TimeSpan.FromSeconds(30), () =>
            {

                if (!isRunning)
                {
                    return;
                }

                if (IsSTAThread)
                {
                    runAction = a;
                    TryLockAndExecute(threadLock, nameof(threadLock), TimeSpan.FromSeconds(30), () =>
                    {
                        Monitor.Pulse(threadLock);
                        Monitor.Wait(threadLock);
                    });
                    if (runException != null) throw runException;
                }
                else
                {
                    TryLockAndExecute(ExecLock, nameof(ExecLock), TimeSpan.FromSeconds(30), () =>
                    {
                        a();
                    });
                }
            });
        }

        /// <summary>
        /// Safely executes an SDK command with return value
        /// </summary>
        /// <param name="func">The SDK command</param>
        /// <returns>the return value of the function</returns>
        public static T ExecuteSafely<T>(Func<T> func)
        {
            T result = default(T);
            ExecuteSafely(delegate { result = func(); });
            return result;
        }

        private static void SafeExecutionLoop()
        {
            TryLockAndExecute(threadLock, nameof(threadLock), TimeSpan.FromSeconds(30), () => 
            {
                Thread cThread = Thread.CurrentThread;
                while (true)
                {
                    Monitor.Wait(threadLock);
                    if (!isRunning)
                    {
                        return;
                    }
                    runException = null;
                    try
                    {
                        TryLockAndExecute(ExecLock, nameof(ExecLock), TimeSpan.FromSeconds(30), () =>
                        {

                            logger.LogInformation("Executing action on ThreadName: {ThreadName}, ApartmentState: {ApartmentState}", cThread.Name, cThread.GetApartmentState());
                            runAction();
                        });
                    }
                    catch (Exception ex)
                    {
                        logger.LogInformation("Exception on ThreadName: {ThreadName}, ApartmentState: {ApartmentState}", cThread.Name, cThread.GetApartmentState());
                        runException = ex;
                    }
                    Monitor.Pulse(threadLock);
                }
            });
        }
    }
}
