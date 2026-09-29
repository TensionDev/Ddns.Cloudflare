using Microsoft.Extensions.Logging;

namespace TensionDev.Ddns.Cloudflare.Tests
{
     /// <summary>
     /// An <see cref="ILogger{TCategoryName}"/> that records the messages it is asked to
     /// log, so tests can assert on the worker's observable lifecycle behaviour (ADR-0002)
     /// without adding a test seam to production code.
     /// </summary>
    internal sealed class CapturingLogger<T> : ILogger<T>
     {
        private readonly object _gate = new();
        private readonly List<string> _messages = new();

        public IReadOnlyList<string> Messages
         {
             get { lock (_gate) { return _messages.ToArray(); } }
         }

        public int MessageCount(string fragment)
         {
             lock (_gate)
              {
                 return _messages.Count(message => message.Contains(fragment));
              }
         }

        public IDisposable BeginScope<TState>(TState state)
          where TState : notnull
         {
             return new NoOpScope();
         }

        public bool IsEnabled(LogLevel logLevel)
         {
             return true;
         }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
         {
             lock (_gate)
              {
                 _messages.Add(formatter(state, exception));
              }
         }

        private sealed class NoOpScope : IDisposable
         {
             public void Dispose()
              {
              }
         }
     }
}
