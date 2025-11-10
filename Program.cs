using System;
using System.Windows.Forms;

namespace ProfileManager
{
	internal static class Program
	{
		[STAThread]
		private static void Main()
		{
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			// run the UI
			Application.Run(new UI.MainForm());
		}
	}
}
