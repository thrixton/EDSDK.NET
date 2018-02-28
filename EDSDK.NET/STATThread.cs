using Microsoft.Extensions.Logging.Summarized;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EDSDK.NET
{
    /// <summary>
    /// Helper class to create or run code on STA threads
    /// </summary>
    public static class STAThread
    {
        public delegate Task ExecuteTask(Action action);

        public delegate void LogAction(string message, params object[] args);

        static LogAction _logInfoAction;
        public static void SetLogInfoAction(LogAction action)
        {
            _logInfoAction = action;
        }

        static void LogInfo(string message, params object[] args)
        {
            if(_logInfoAction != null)
            {
                _logInfoAction(message, args);
            }
        }

        public static ExecuteTask OverrideTaskExecutor { get; set; }



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
        /// alt, main task instead of thread
        /// </summary>
        private static Task mainTask;

        private static ManualResetEvent mainTaskEndSignal = new ManualResetEvent(false);


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
        internal static void Init(SummarizedLogger logger)
        {
            if (!isRunning)
            {
                if (OverrideTaskExecutor != null)
                {
                    mainTask = OverrideTaskExecutor.Invoke(new Action(()=> SafeExecutionLoop(logger)));
                }
                else
                {
                    main = Create(new Action(()=> SafeExecutionLoop(logger)));
                    main.Start();
                }
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
                lock (threadLock) { Monitor.Pulse(threadLock); }
                main.Join();
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
            LogInfo("Created STA Thread. ThreadName: {ThreadName}, ApartmentState: {ApartmentState}", thread.Name, thread.GetApartmentState());
            return thread;
        }


        /// <summary>
        /// Safely executes an SDK command
        /// </summary>
        /// <param name="a">The SDK command</param>
        public static void ExecuteSafely(Action a)
        {
            lock (runLock)
            {
                if (!isRunning)
                {
                    return;
                }

                if (IsSTAThread)
                {
                    runAction = a;
                    lock (threadLock)
                    {
                        Monitor.Pulse(threadLock);
                        Monitor.Wait(threadLock);
                    }
                    if (runException != null) throw runException;
                }
                else
                {
                    lock (ExecLock)
                    {
                        a();
                    }
                }
            }
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

        private static void SafeExecutionLoop(SummarizedLogger logger)
        {
            lock (threadLock)
            {
                Thread cThread = Thread.CurrentThread;
                while (true)
                {

                    logger.LogEvent("SafeExecutionLoop.StartLoop");

                    Monitor.Wait(threadLock);
                    if (!isRunning)
                    {
                        mainTaskEndSignal.Set();
                        return;
                    }
                    runException = null;
                    try
                    {
                        lock (ExecLock)
                        {
                            LogInfo("Executing action on ThreadName: {ThreadName}, ApartmentState: {ApartmentState}", cThread.Name, cThread.GetApartmentState());
                            logger.LogEvent("SafeExecutionLoop.RunAction");
                            runAction();
                        }
                    }
                    catch (Exception ex)
                    {
                        LogInfo("Exception on ThreadName: {ThreadName}, ApartmentState: {ApartmentState}", cThread.Name, cThread.GetApartmentState());
                        runException = ex;
                    }
                    Monitor.Pulse(threadLock);
                }
            }

        }
    }
}
