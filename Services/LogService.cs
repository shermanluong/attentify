using System.Collections.Concurrent;
using System.Threading;
using Serilog;

namespace GoogleLogin.Services
{
    public class LoggerService
    {
        private readonly ConcurrentQueue<string> _logQueue = new();
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private readonly Task _loggingTask;

        public LoggerService()
        {
            _loggingTask = Task.Run(() => ProcessQueue(_cancellationTokenSource.Token));
        }

        public void Log(string message)
        {
            _logQueue.Enqueue(message);
        }

        private async Task ProcessQueue(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (_logQueue.TryDequeue(out var logMessage))
                {
                    Log(logMessage);
                }

                await Task.Delay(100); // Wait a bit before checking the queue again
            }
        }

        public void Stop()
        {
            _cancellationTokenSource.Cancel();
            _loggingTask.Wait();
        }
    }
}