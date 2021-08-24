using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

namespace IL2CS.Generator
{
	public class LoggingScope : IDisposable
	{
		public ILoggerFactory Factory { get; private set; }

		public LoggingScope()
		{
			Factory = LoggerFactory.Create(builder => builder.AddSimpleConsole(options =>
			{
				options.IncludeScopes = true;
				options.TimestampFormat = "hh:mm:ss ";
			}));
		}

		public ILogger<T> CreateLogger<T>()
		{
			return Factory.CreateLogger<T>();
		}

		public void Dispose()
		{
			Factory.Dispose();
		}

	}
}
