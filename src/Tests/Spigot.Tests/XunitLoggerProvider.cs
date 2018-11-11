using System;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Spigot.Tests
{
    internal class XunitLoggerProvider : ILoggerProvider
    {
        private ITestOutputHelper _outputHelper;

        public XunitLoggerProvider(ITestOutputHelper outputHelper)
        {
            _outputHelper = outputHelper;
        }

        public void Dispose()
        {
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new XunitLogger(_outputHelper);
        }
    }
}