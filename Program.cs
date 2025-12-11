using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ProfileManager.Benchmarks;
using ProfileManager.Profiles;

namespace ProfileManager
{
	internal static class Program
	{
		[STAThread]
		private static void Main()
		{
			var args = Environment.GetCommandLineArgs();
			bool benchMode = args.Any(a => string.Equals(a, "--bench", StringComparison.OrdinalIgnoreCase) || string.Equals(a, "-bench", StringComparison.OrdinalIgnoreCase) || string.Equals(a, "bench", StringComparison.OrdinalIgnoreCase));
			if (benchMode)
			{
				ConsoleHelper.EnsureConsole();
				var benchRoot = Path.Combine(AppContext.BaseDirectory, "bench_data");
				RangeScanBenchmark.Run(benchRoot);
				return;
			}

			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			// run the UI
			Application.Run(new UI.MainForm());
		}
	}

	internal static class ConsoleHelper
	{
		private const int ATTACH_PARENT_PROCESS = -1;
		[DllImport("kernel32.dll", SetLastError = true)] private static extern bool AttachConsole(int dwProcessId);
		[DllImport("kernel32.dll", SetLastError = true)] private static extern bool AllocConsole();

		public static void EnsureConsole()
		{
			// Try to attach to parent; if none, allocate a new console so WriteLine shows up.
			if (!AttachConsole(ATTACH_PARENT_PROCESS))
			{
				AllocConsole();
			}
		}
	}
}
