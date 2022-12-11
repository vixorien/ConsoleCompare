using System;
using EnvDTE;
using System.IO;
using System.Diagnostics;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using EnvDTE80;
using System.Windows.Media;
using Microsoft.VisualStudio.PlatformUI;

namespace ConsoleCompare
{
	internal class CaptureManager
	{

		private DTE dte;
		private AsyncPackage package;
		private ResultsWindow window;
		private StreamWriter procInputWriter;
		private System.Diagnostics.Process proc;

		private ConsoleSimile simile;

		public CaptureManager(AsyncPackage package, ResultsWindow window)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			this.package = package;
			this.window = window;

			dte = Package.GetGlobalService(typeof(DTE)) as DTE;
		}


		/// <summary>
		/// Begins a capture of the current project's output
		/// </summary>
		public void BeginCapture(ConsoleSimile simile)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			// Overwrite the current simile for comparison
			this.simile = simile;
			if (this.simile == null) 
				throw new ArgumentNullException("Simile cannot be null for a capture");

			// Is the process alive and in progress?
			if (proc != null && !proc.HasExited)
			{
				// Kill it to start fresh
				proc.Kill();
				proc.Dispose();
			}

			// Rebuild solution (wait for it to finish)
			window.SetStatus("Building application");
			dte.Solution.SolutionBuild.Build(true);

			// Grab the exe path and verify
			string exePath = FindPathToExecutable();
			if (!File.Exists(exePath))
			{
				MessageBox(
					"Cannot run output capture; compiled executable not found: " + exePath,
					"Error");
				return;
			}

			// Reset
			simile.ResetOutputIteration();
			window.ClearAllText();

			// Create the process
			proc = new System.Diagnostics.Process();

			// Set up events
			proc.EnableRaisingEvents = true;
			proc.OutputDataReceived += Capture_DataReceived;
			proc.ErrorDataReceived += Capture_ErrorReceived;
			proc.Exited += Capture_ProcessExit;

			// Set up start info and redirects
			proc.StartInfo.FileName = exePath;
			proc.StartInfo.CreateNoWindow = true;
			proc.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
			proc.StartInfo.UseShellExecute = false;
			proc.StartInfo.RedirectStandardError = true;
			proc.StartInfo.RedirectStandardOutput = true;
			proc.StartInfo.RedirectStandardInput = true;

			// Get rolling
			proc.Start();
			proc.BeginErrorReadLine();
			proc.BeginOutputReadLine();
			procInputWriter = proc.StandardInput;

			window.SetStatus("Application started");

			// Note: Do NOT block the process here using WaitForExit(), as that
			// will cause problems with the threaded nature of the UI system

			// Potential new tactic for handling input/output hangs with
			// write() and ReadLine() combos: https://stackoverflow.com/a/29118547

			// Loop and send all input to the writer immediately so
			// that the process can read it as necessary
			simile.SendAllInput(procInputWriter);
		}


		private void Capture_DataReceived(object sender, DataReceivedEventArgs e)
		{
			// This is most likely on a different thread, so we need to swap
			// to the main thread before doing any UI work
			ThreadHelper.JoinableTaskFactory.Run(async delegate
			{
				await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

				// Grab the output and expected output
				string output = e.Data;
				string expected = simile.GetNextOutput();

				// Do they match?
				SolidColorBrush color = (output == expected) ? Brushes.Green : Brushes.OrangeRed;
				
				// Add the text to both boxes
				window.AddTextOutput(output, color);
				window.AddTextExpected(expected);
			});
		}


		private void Capture_ErrorReceived(object sender, DataReceivedEventArgs e)
		{
			// Verify we have something, otherwise skip
			if (string.IsNullOrEmpty(e.Data))
				return;

			// This is most likely on a different thread, so we need to swap
			// to the main thread before doing any UI work
			ThreadHelper.JoinableTaskFactory.Run(async delegate
			{
				await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
				window.AddTextOutput(e.Data, Brushes.Red);
			});
		}

		

		private void Capture_ProcessExit(object sender, EventArgs e)
		{
			// This is most likely on a different thread, so we need to swap
			// to the main thread before doing any UI work
			ThreadHelper.JoinableTaskFactory.Run(async delegate
			{
				await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

				// I think the process is the sender of this event?
				System.Diagnostics.Process proc = sender as System.Diagnostics.Process;

				// TODO: Track the lifetime of the process and update the UI accordingly
				// - Maybe a stop/start button?

				window.SetStatus("Application exited");
			});
		}

		/// <summary>
		/// Helper for finding the path to the first currently loaded project's built executable
		/// </summary>
		/// <returns>Full path to the executable of the (first) current project</returns>
		private string FindPathToExecutable()
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			// Find the first project using a foreach loop, as using
			// the .Item(0) indexing was problematic
			Project firstProject = null;
			foreach (Project p in dte.Solution.Projects)
			{
				// Dirty, but relying on foreach
				// enumeration due to issues with .Item()
				if (firstProject == null)
				{
					firstProject = p;
					break;
				}
			}

			// Path creation
			// From: https://social.msdn.microsoft.com/Forums/vstudio/en-US/03d9d23f-e633-4a27-9b77-9029735cfa8d/how-to-get-the-right-8220output-path8221-from-envdteproject-by-code-if-8220show-advanced?forum=vsx
			string fullPath = firstProject.Properties.Item("FullPath").Value.ToString();
			string outputPath = firstProject.ConfigurationManager.ActiveConfiguration.Properties.Item("OutputPath").Value.ToString();
			string filename = firstProject.Properties.Item("OutputFileName").Value.ToString();

			string exePath = Path.Combine(fullPath, outputPath, filename);

			// Quick check to verify that we're not looking at an assembly
			if (exePath.EndsWith(".dll"))
				exePath = exePath.Replace(".dll", ".exe");

			return exePath;
		}

		/// <summary>
		/// Helper for showing a message box to the user
		/// </summary>
		/// <param name="message">The message displayed in the box</param>
		/// <param name="title">The title of the box</param>
		private void MessageBox(string message, string title = "Message")
		{
			VsShellUtilities.ShowMessageBox(
				package,
				message,
				title,
				OLEMSGICON.OLEMSGICON_INFO,
				OLEMSGBUTTON.OLEMSGBUTTON_OK,
				OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
		}
	}
}
