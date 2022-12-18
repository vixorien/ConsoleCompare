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

namespace ConsoleCompare
{
	internal class CaptureManager
	{
		public static SolidColorBrush BackgroundColor = Brushes.Black;
		public static SolidColorBrush ExpectedOutputColor = Brushes.White;
		public static SolidColorBrush MatchingOutputColor = Brushes.Green;
		public static SolidColorBrush NonmatchingOutputColor = Brushes.OrangeRed;
		public static FontStyle OutputFontStyle = FontStyles.Normal;
		public static FontWeight OutputFontWeight = FontWeights.Normal;
		public static FontStyle InputFontStyle = FontStyles.Italic;
		public static FontWeight InputFontWeight = FontWeights.Bold;
		public static bool InvertInputColors = false;


		private DTE dte;
		private ResultsWindow window;
		private System.Diagnostics.Process proc;

		private ConsoleSimile simile;

		public CaptureManager(ResultsWindow window)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			this.window = window;

			dte = Package.GetGlobalService(typeof(DTE)) as DTE;

			// For reference: Use this to hook up solution-related events (like opening, closing, etc.)
			IVsSolution solution = ServiceProvider.GlobalProvider.GetService(typeof(SVsSolution)) as IVsSolution;
			//solution.AdviseSolutionEvents(...);
		}


		/// <summary>
		/// Begins a capture of the current project's output
		/// </summary>
		public void BeginCapture(ConsoleSimile simile)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			// Turn off the capture button to prevent a second simultaneous run
			window.CaptureButtonEnabled = false;

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
			window.ClearAllText();

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
			System.Threading.Thread t = new System.Threading.Thread(
				() => ManualIO()
			);
			t.Start();
			window.SetStatus("Application started");

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

			// Track the previous line's ending to know if the next has to append
			LineEndingType previousLineEnding = LineEndingType.NewLine;

			// Track the match count as we go so we can report after
			int lineCount = 0;
			int matchCount = 0;

			// Loop thorugh all simile lines and check against the process's output
			for (int i = 0; i < simile.Count; i++)
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

						// Grab the expected output and check the line ending type
						string expected = output.Text;
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
									charCount < expected.Length && 
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
						bool match = actual == expected;
						SolidColorBrush color = match ? MatchingOutputColor : NonmatchingOutputColor;
						if (match)
							matchCount++;

						// Swap to the UI thread to update
						ThreadHelper.JoinableTaskFactory.Run(async delegate
						{
							await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

							// Add the text to both boxes
							window.AddTextOutput(actual, color, BackgroundColor, OutputFontStyle, OutputFontWeight, append);
							window.AddTextExpected(expected, ExpectedOutputColor, BackgroundColor, OutputFontStyle, OutputFontWeight, append);
						});

						// Save the previous ending
						previousLineEnding = output.LineEnding;

						break;

					// Line is input from the user
					case SimileLineInput input:

						// Grab the data to send to the process, do so and put in both boxes
						proc.StandardInput.WriteLine(input.Text);

						// Check for color inversion
						SolidColorBrush foreOutput = InvertInputColors ? BackgroundColor : MatchingOutputColor;
						SolidColorBrush backOutput = InvertInputColors ? MatchingOutputColor : BackgroundColor;
						SolidColorBrush foreExpected = InvertInputColors ? BackgroundColor : ExpectedOutputColor;
						SolidColorBrush backExpected = InvertInputColors ? ExpectedOutputColor : BackgroundColor;

						// Swap to the UI thread to update
						ThreadHelper.JoinableTaskFactory.Run(async delegate
						{
							await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

							// Add the text to both boxes
							window.AddTextOutput(input.Text, foreOutput, backOutput, InputFontStyle, InputFontWeight, append);
							window.AddTextExpected(input.Text, foreExpected, backExpected, InputFontStyle, InputFontWeight, append);
						});

						// Previous line ending is now a new line since we're simulating the user pressing enter
						previousLineEnding = LineEndingType.NewLine;

						// Assume input lines always match since we do those ourselves,
						// though only if we're not appending to another line
						if (!append)
							matchCount++;

						break;
				}
			}

			// Swap to the UI thread to update
			ThreadHelper.JoinableTaskFactory.Run(async delegate
			{
				await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

				// All done, re-enable the button and update the status bar
				window.CaptureButtonEnabled = true;
				window.SetStatus($"Application finished - {matchCount}/{lineCount} lines match");
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
				Command.Instance.ServiceProviderPackage as AsyncPackage,
				message,
				title,
				OLEMSGICON.OLEMSGICON_INFO,
				OLEMSGBUTTON.OLEMSGBUTTON_OK,
				OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
		}

	
	}
}
