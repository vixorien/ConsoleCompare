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

		private DTE dte;
		private ResultsWindow window;
		private System.Diagnostics.Process proc;

		private ConsoleSimile simile;

		public CaptureManager(ResultsWindow window)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

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

			bool asyncIO = false;

			// Create the process
			proc = new System.Diagnostics.Process();

			// Set up events if async
			if (asyncIO)
			{
				proc.EnableRaisingEvents = true;
				proc.Exited += Capture_ProcessExit;
				proc.OutputDataReceived += Capture_DataReceived;
				proc.ErrorDataReceived += Capture_ErrorReceived;
			}

			// Set up start info and redirects
			proc.StartInfo.FileName = exePath;
			proc.StartInfo.CreateNoWindow = true;
			proc.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
			proc.StartInfo.UseShellExecute = false;
			proc.StartInfo.RedirectStandardError = true;
			proc.StartInfo.RedirectStandardOutput = true;
			proc.StartInfo.RedirectStandardInput = true;

			// Get rolling
			// Note: Do NOT block the process here using WaitForExit(), as that
			// will cause problems with the threaded nature of the UI system
			proc.Start();
			if (asyncIO)
			{
				proc.BeginErrorReadLine();
				proc.BeginOutputReadLine();
			}

			window.SetStatus("Application started");

			// Testing
			//MessageBox(CheckCodeForComments(), "Elements");

			// If we're manually handling IO, 
			if (!asyncIO)
			{
				System.Threading.Thread t = new System.Threading.Thread(
					() => ManualIO()
				);
				t.Start();
			}
			else
			{
				// Loop and send all input to the writer immediately so
				// that the process can read it as necessary
				simile.SendAllInput(proc.StandardInput);
			}
		}


		private void ManualIO()
		{
			// Track the previous line's ending to know if the next has to append
			LineEndingType previousLineEnding = LineEndingType.NewLine;

			// Loop thorugh all simile lines and check against the process's output
			for (int i = 0; i < simile.Count; i++)
			{
				// Will we be appending this line?
				bool append = previousLineEnding == LineEndingType.SameLine;

				// Grab the current line and check the type
				SimileLine line = simile[i];
				switch (line)
				{
					// Line is output from the console process
					case SimileLineOutput output:

						// Grab the expected output and check the line ending type
						string expected = output.Text;
						string actual = null;
						//actual = proc.StandardOutput.ReadLine();
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
						SolidColorBrush color = (actual == expected) ? Brushes.Green : Brushes.OrangeRed;

						// Swap to the UI thread to update
						ThreadHelper.JoinableTaskFactory.Run(async delegate
						{
							await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

							// Add the text to both boxes
							window.AddTextOutput(actual, color, FontStyles.Normal, FontWeights.Normal, append);
							window.AddTextExpected(expected, Brushes.White, FontStyles.Normal, FontWeights.Normal, append);
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

							// Add the text to both boxes
							window.AddTextOutput(input.Text, Brushes.Green, FontStyles.Italic, FontWeights.UltraBold, append);
							window.AddTextExpected(input.Text, Brushes.White, FontStyles.Italic, FontWeights.UltraBold, append);
						});

						// Previous line ending is now a new line since we're simulating the user pressing enter
						previousLineEnding = LineEndingType.NewLine;

						break;
				}
			}
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
				//window.AddTextOutput(output, color);
				//window.AddTextExpected(expected);
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
				//window.AddTextOutput(e.Data, Brushes.Red);
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
				Command.Instance.ServiceProviderPackage as AsyncPackage,
				message,
				title,
				OLEMSGICON.OLEMSGICON_INFO,
				OLEMSGBUTTON.OLEMSGBUTTON_OK,
				OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
		}

		/// <summary>
		/// Temporary placeholder for comment checking syntax
		/// </summary>
		private string CheckCodeForComments()
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			string result = "";

			Project proj = null;
			foreach (Project p in dte.Solution.Projects)
			{
				// Get first project
				// Yes, the .Items(0) syntax should work, but has been failing
				proj = p;
				break;
			}

			// Go through all project items looking for code files
			foreach (ProjectItem item in proj.ProjectItems)
			{
				if (item.FileCodeModel == null)
					continue;

				// Each code element
				foreach (CodeElement element in item.FileCodeModel.CodeElements)
				{
					result += WalkCodeTree(element, result, 0);
				}
			}

			return result;
		}

		/// <summary>
		/// Recursive function for checking all code elements for comments
		/// </summary>
		/// <param name="element"></param>
		/// <param name="results"></param>
		/// <param name="depth"></param>
		/// <returns></returns>
		private string WalkCodeTree(CodeElement element, string results, int depth)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			// Loop through each element recursively and add info about the element
			string elementDetail = "Element: " + element.Kind + "\n";
			results += elementDetail.PadLeft(elementDetail.Length + depth, '-');


			// Check for function
			if (element.Kind == vsCMElement.vsCMElementFunction)
			{
				// Check for an actual project element (and not external)
				if (element.InfoLocation == vsCMInfoLocation.vsCMInfoLocationProject)
				{
					// Cast as function element?
					CodeFunction func = element as CodeFunction;
					if (func != null)
					{
						results += $"FOUND FUNCTION ELEMENT: {func.Name}\n";
						results += "Comments? " + (string.IsNullOrEmpty(func.Comment) ? "No\n" : "Yes\n"); // Regular (non-xml) comments
						results += "Doc Comments: " + (string.IsNullOrEmpty(func.DocComment) ? "No\n" : "Yes\n"); // XML comments
					}
				}
			}

			foreach (CodeElement child in element.Children)
			{
				results = WalkCodeTree(child, results, depth + 1);
			}

			return results;
		}
	}
}
