using System;
using EnvDTE;
using System.IO;
using System.Diagnostics;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using EnvDTE80;
using System.Windows.Media;
using Microsoft.VisualStudio.PlatformUI;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.VisualStudio.VCProjectEngine;
using System.Windows;
using Microsoft.VisualStudio;
using System.Security.AccessControl;
using System.Windows.Forms;
using VSLangProj;
using Microsoft.VisualStudio.Imaging;
using System.Timers;
using Microsoft.VisualStudio.Imaging.Interop;

// Known monikers preview: http://glyphlist.azurewebsites.net/knownmonikers/

namespace ConsoleCompare
{
	internal class CaptureManager //: IVsSolutionEvents // <-- Only necessary if we're registering solution/project events
	{
		// Constants for capture output and options
		private const int ProcessTimeoutSeconds = 5;
		private const string ProcessTimeoutMessage = "Process taking a while; probable input/output mismatch or infinite loop";

		private const string StatusProcessStoppedByUser = "Comparison stopped early by user";
		private const string TextProcessStoppedByUser = "Process stopped by user";
		private readonly ImageMoniker IconProcessStoppedByUser = KnownMonikers.StatusStopped;


		// Visual studio-level stuff
		private DTE dte;
		private ResultsWindow window;
		//private uint solutionEventsCookie; // <-- Needed if registering for events

		// Process stuff
		private ConsoleSimile simile; 
		private System.Diagnostics.Process proc;
		private System.Threading.Thread procThread;
		private bool killThread; // Not the safest, but should work for our purpose of ending a thread
		private System.Timers.Timer processTimeoutTimer;
		
		/// <summary>
		/// Creates a capture manager for capturing and comparing console output
		/// </summary>
		/// <param name="window">The window that the capture uses</param>
		public CaptureManager(ResultsWindow window)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			this.window = window;
			dte = Package.GetGlobalService(typeof(DTE)) as DTE;

			// Set up the timer for process timeout
			processTimeoutTimer = new System.Timers.Timer(ProcessTimeoutSeconds * 1000);
			processTimeoutTimer.Elapsed += ProcessTimeoutTimer_Elapsed;

			// For reference: Use this to hook up solution-related events (like opening, closing, etc.)
			//IVsSolution solution = ServiceProvider.GlobalProvider.GetService(typeof(SVsSolution)) as IVsSolution;
			//solution?.AdviseSolutionEvents(this, out solutionEventsCookie);
		}


		/// <summary>
		/// Begins a capture of the current project's output
		/// </summary>
		public void BeginCapture(ConsoleSimile simile)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			// Verify we can actually capture
			if (!VerifyValidProject())
				return;

			// Turn off the capture button to prevent a second simultaneous run
			window.CaptureButtonEnabled = false;
			window.StopButtonEnabled = true;
			window.OpenButtonEnabled = false;

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
			window.SetStatus("Building application", KnownMonikers.BuildSolution);
			dte.Solution.SolutionBuild.Build(true);

			// Grab the exe path and verify
			string exePath = FindPathToExecutable();
			if (!File.Exists(exePath))
			{
				MessageBox(
					"Cannot run output capture; compiled executable not found: " + exePath,
					"Error");
				window.CaptureButtonEnabled = true;
				window.StopButtonEnabled = false;
				window.OpenButtonEnabled = true;
				return;
			}

			// Reset
			window.ClearAllOutputText();

			// Create the process
			proc = new System.Diagnostics.Process();

			// Set up start info and redirects
			proc.StartInfo.FileName = exePath;
			proc.StartInfo.CreateNoWindow = true;
			proc.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
			proc.StartInfo.UseShellExecute = false;
			proc.StartInfo.RedirectStandardError = true;
			proc.StartInfo.RedirectStandardOutput = true;
			proc.StartInfo.RedirectStandardInput = true;

			// Handle IO in a synchronous manner, but on another thread
			// This function will start the process
			killThread = false;
			procThread = new System.Threading.Thread(
				() => ManualIO()
			);
			procThread.Start();
			window.SetStatus("Application started", KnownMonikers.StatusRunning);

			window.BeginRunStatusAnimation();

		}

		/// <summary>
		/// Manually processes the input/output of the console process and compares
		/// it against the console simile.
		/// </summary>
		private void ManualIO()
		{
			// Start the process here so we don't have to wait for the thread to start up
			// Note: Do NOT block the process here using WaitForExit(), as that
			// will cause problems with the threaded nature of the UI system
			proc.Start();

			// Start the timer
			processTimeoutTimer.Start();

			// Track the previous line's ending to know if the next has to append
			LineEndingType previousLineEnding = LineEndingType.NewLine;

			// Track the match count as we go so we can report after
			int lineCount = 0;
			int matchCount = 0;

			// Loop thorugh all simile lines and check against the process's output
			for (int i = 0; i < simile.Count && !killThread; i++)
			{
				// Will we be appending this line?
				bool append = previousLineEnding == LineEndingType.SameLine;
				if (!append)
					lineCount++;

				// Grab the current line and check the type
				SimileLine line = simile[i];
				switch (line)
				{
					// Line is output from the console process
					case SimileLineOutput output:

						// Create the actual text based on line ending
						string actual = null;
						switch (output.LineEnding)
						{
							// New line, so just perform a standard ReadLine()
							case LineEndingType.NewLine: actual = proc.StandardOutput.ReadLine(); break;

							// Output expects the next line (probably input) to be on the same line,
							// so we can't rely on ReadLine() for this.  Need to manually grab characters.
							case LineEndingType.SameLine:

								actual = "";
								int charCount = 0;
								while(
									output.RawText != actual &&
									!proc.StandardOutput.EndOfStream && 
									proc.StandardOutput.Peek() != -1
								)
								{
									actual += (char)proc.StandardOutput.Read();
									charCount++;
								}

								// TODO: Handle the case when we run out of characters before the end!
								break;
						}

						// Do they match?
						bool match = output.CompareLine(actual);
						string expectedReport = match ? actual : output.RawText; // What text to report to the user?
						if (match)
							matchCount++;

						// Swap to the UI thread to update
						ThreadHelper.JoinableTaskFactory.Run(async delegate
						{
							await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

							// Add the text to both boxes
							if (!killThread)
							{
								window.AddTextOutput(actual, ResultsTextType.Output, append, match);
								window.AddTextExpected(expectedReport, ResultsTextType.Output, append, match);
							}
						});

						// Save the previous ending
						previousLineEnding = output.LineEnding;

						break;

					// Line is input from the user
					case SimileLineInput input:

						// Grab the data to send to the process, do so and put in both boxes
						proc.StandardInput.WriteLine(input.Text);

						// Swap to the UI thread to update
						ThreadHelper.JoinableTaskFactory.Run(async delegate
						{
							await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

							// Add the text to both boxes (assuming a match since we're providing the input)
							if (!killThread)
							{
								window.AddTextOutput(input.Text, ResultsTextType.Input, append, true);
								window.AddTextExpected(input.Text, ResultsTextType.Input, append, true);
							}
						});

						// Previous line ending is now a new line since we're simulating the user pressing enter
						previousLineEnding = LineEndingType.NewLine;

						// Assume input lines always match since we do those ourselves,
						// though only if we're not appending to another line
						if (!append)
							matchCount++;

						break;
				}

				// Kill the thread early?
				if (killThread)
					break;
			}

			// Once we're out of the loop, kill the timer
			processTimeoutTimer.Stop();

			// Swap to the UI thread to update
			ThreadHelper.JoinableTaskFactory.Run(async delegate
			{
				await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

				// All done, re-enable the button and update the status bar
				window.CaptureButtonEnabled = true;
				window.StopButtonEnabled = false;
				window.OpenButtonEnabled = true;
				window.EndRunStatusAnimation();

				if (killThread)
				{
					window.SetStatus(StatusProcessStoppedByUser, IconProcessStoppedByUser);
					window.AddTextOutput(TextProcessStoppedByUser, ResultsTextType.Output, false, false);
					window.AddTextExpected(TextProcessStoppedByUser, ResultsTextType.Output, false, false);
				}
				else
				{
					window.SetStatus(
						$"Comparison finished - {matchCount}/{lineCount} lines match",
						matchCount == lineCount ? KnownMonikers.StatusOK : KnownMonikers.StatusError);
				}
			});

			killThread = false;
		}


		public void ProcessTimeoutTimer_Elapsed(object source, ElapsedEventArgs e)
		{
			processTimeoutTimer.Stop();

			// Swap to the UI thread to update
			ThreadHelper.JoinableTaskFactory.Run(async delegate
			{
				await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

				// The process has been running for a while, so notify the user and disable the timer
				window.SetStatusNoIconChange(ProcessTimeoutMessage);
			});
		}


		/// <summary>
		/// Stops a capture in progress, if one exists
		/// </summary>
		public bool StopCapture()
		{
			// Is there a thread going at all?
			if (procThread == null || !procThread.IsAlive)
				return false;

			// Attempt to kill the process and the ManualIO thread
			proc.Kill();
			killThread = true;
			return true;
		}


		/// <summary>
		/// Helper for finding the path to the first currently loaded project's built executable
		/// </summary>
		/// <returns>Full path to the executable of the (first) current project</returns>
		public string FindPathToExecutable()
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			// Find the first project and verify
			Project firstProject = GetFirstProject();
			if (firstProject == null)
				return null;

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
		/// Helper for finding the path to the first project's folder
		/// </summary>
		/// <returns>The path to the first project's folder</returns>
		public string FindPathToProjectFolder()
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			// Find the first project and verify
			Project firstProject = GetFirstProject();
			if (firstProject == null)
				return null;

			return firstProject.Properties.Item("FullPath").Value.ToString();
		}

		/// <summary>
		/// Helper for showing a message box to the user
		/// </summary>
		/// <param name="message">The message displayed in the box</param>
		/// <param name="title">The title of the box</param>
		private void MessageBox(string message, string title = "Message")
		{
			VsShellUtilities.ShowMessageBox(
				Command.Instance.ServiceProviderPackage as AsyncPackage,
				message,
				title,
				OLEMSGICON.OLEMSGICON_INFO,
				OLEMSGBUTTON.OLEMSGBUTTON_OK,
				OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
		}

		/// <summary>
		/// Verifies that we have everything we need (a solution and
		/// a console project) to proceed.
		/// </summary>
		/// <returns></returns>
		private bool VerifyValidProject()
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			// Is there a solution?
			if (dte.Solution == null)
			{
				MessageBox("No solution loaded; please load a solution with a console application.", "Error");
				return false;
			}

			// Is there a project?
			if (dte.Solution.Projects.Count == 0)
			{
				MessageBox("No projects loaded; please load a console application project.", "Error");
				return false;
			}

			// Is it the right type of project?
			Project firstProject = GetFirstProject();
			Property outputType = firstProject.Properties.Item("OutputType");
			prjOutputType projectType = (prjOutputType)outputType.Value;
			if (projectType != prjOutputType.prjOutputTypeExe)
			{
				MessageBox(
					"First project in solution is not a standard console application; please load a console application project.",
					"Error");
				return false;
			}

			// Valid project
			return true;
		}

		/// <summary>
		/// Gets the first project in the current solution
		/// </summary>
		/// <returns>The first project, or null if no project/solution exist</returns>
		private Project GetFirstProject()
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			// Verify a solution
			if (dte.Solution == null)
				return null;

			// Find the first project using a foreach loop, as using
			// the .Item(0) indexing was problematic.  Seems like the first
			// project is index 1, which either means the overall indexing is
			// 1-based (weird) or there is some other object sitting at index 0
			foreach (Project p in dte.Solution.Projects)
			{
				// Dirty, but relying on enumeration due to issues with .Item()
				return p;
			}

			// No projects
			return null;
		}

		//public int OnAfterOpenProject(IVsHierarchy pHierarchy, int fAdded)
		//{
		//	MessageBox("AFTER OPEN PROJECT");
		//	return VSConstants.S_OK;
		//}

		//public int OnQueryCloseProject(IVsHierarchy pHierarchy, int fRemoving, ref int pfCancel)
		//{
		//	return VSConstants.S_OK;
		//}

		//public int OnBeforeCloseProject(IVsHierarchy pHierarchy, int fRemoved)
		//{
		//	return VSConstants.S_OK;
		//}

		//public int OnAfterLoadProject(IVsHierarchy pStubHierarchy, IVsHierarchy pRealHierarchy)
		//{
		//	MessageBox("AFTER LOAD PROJECT");
		//	return VSConstants.S_OK;
		//}

		//public int OnQueryUnloadProject(IVsHierarchy pRealHierarchy, ref int pfCancel)
		//{
		//	return VSConstants.S_OK;
		//}

		//public int OnBeforeUnloadProject(IVsHierarchy pRealHierarchy, IVsHierarchy pStubHierarchy)
		//{
		//	return VSConstants.S_OK;
		//}

		//public int OnAfterOpenSolution(object pUnkReserved, int fNewSolution)
		//{
		//	MessageBox("AFTER OPEN SOLUTION");
		//	return VSConstants.S_OK;
		//}

		//public int OnQueryCloseSolution(object pUnkReserved, ref int pfCancel)
		//{
		//	return VSConstants.S_OK;
		//}

		//public int OnBeforeCloseSolution(object pUnkReserved)
		//{
		//	return VSConstants.S_OK;
		//}

		//public int OnAfterCloseSolution(object pUnkReserved)
		//{
		//	return VSConstants.S_OK;
		//}
	}
}
