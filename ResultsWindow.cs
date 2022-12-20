using Microsoft.VisualStudio.Shell;
using System;
using System.Runtime.InteropServices;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using System.IO;
using Microsoft.VisualStudio.Imaging;

namespace ConsoleCompare
{
	/// <summary>
	/// This class implements the tool window exposed by this package and hosts a user control.
	/// </summary>
	/// <remarks>
	/// In Visual Studio tool windows are composed of a frame (implemented by the shell) and a pane,
	/// usually implemented by the package implementer.
	/// <para>
	/// This class derives from the ToolWindowPane class provided from the MPF in order to use its
	/// implementation of the IVsUIElementPane interface.
	/// </para>
	/// </remarks>
	[Guid("3671dfb2-140b-4c50-b9ec-9891d8eb6002")]
	public class ResultsWindow : ToolWindowPane
	{
		/// <summary>
		/// The capture manager for running and interacting with the compiled app
		/// </summary>
		private CaptureManager capture;

		/// <summary>
		/// The actual window control with all of the UI components
		/// </summary>
		private ResultsWindowControl windowControl;

		/// <summary>
		/// The current simile for console comparison
		/// </summary>
		private ConsoleSimile currentSimile;

		/// <summary>
		/// The file that corresponds to the current simile
		/// </summary>
		private string currentSimileFile;


		/// <summary>
		/// Gets or sets the state of the capture button
		/// </summary>
		public bool CaptureButtonEnabled
		{
			get => windowControl.ButtonCapture.IsEnabled;
			set
			{
				// Set the image to the opposite value (disabled button == grayscale)
				windowControl.ButtonCapture.IsEnabled = value;
				(windowControl.ButtonCapture.Content as CrispImage).Grayscale = !value;
			}
		}

		/// <summary>
		/// Gets or sets the state of the capture button
		/// </summary>
		public bool StopButtonEnabled
		{
			get => windowControl.ButtonStop.IsEnabled;
			set
			{
				// Set the image to the opposite value (disabled button == grayscale)
				windowControl.ButtonStop.IsEnabled = value;
				( windowControl.ButtonStop.Content as CrispImage).Grayscale = !value;
			}
		}

		/// <summary>
		/// Gets or sets the state of the capture button
		/// </summary>
		public bool OpenButtonEnabled
		{
			get => windowControl.ButtonLoadSimile.IsEnabled;
			set
			{
				// Set the image to the opposite value (disabled button == grayscale)
				windowControl.ButtonLoadSimile.IsEnabled = value;
				(windowControl.ButtonLoadSimile.Content as CrispImage).Grayscale = !value;
			}
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ResultsWindow"/> class.
		/// </summary>
		public ResultsWindow() : base(null)
		{
			this.Caption = "Console Capture";

			// This is the user control hosted by the tool window; Note that, even if this class implements IDisposable,
			// we are not calling Dispose on this object. This is because ToolWindowPane calls Dispose on
			// the object returned by the Content property.
			this.windowControl = new ResultsWindowControl(this);
			this.Content = windowControl; // Casts to object

			// Create the capture manager with a reference to this window
			capture = new CaptureManager(this);
			SetStatus("Extension Loaded");

			// No simile yet, so no capture yet
			currentSimile = null;
			currentSimileFile = null;
			CaptureButtonEnabled = false;
			StopButtonEnabled = false;
			OpenButtonEnabled = true;
		}

		/// <summary>
		/// Shows an open file dialog and loads a simile file (if chosen)
		/// </summary>
		public void LoadSimileUsingFileDialog()
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			// Set up the dialog
			OpenFileDialog open = new OpenFileDialog();
			open.InitialDirectory = capture.FindPathToProjectFolder();
			open.Filter = "Console Compare Simile Files|*.simile";

			// Show and check result
			bool? result = open.ShowDialog();
			if (result.HasValue && result.Value == true)
			{
				// Parse and check
				currentSimile = SimileParser.ParseFromFile(open.FileName);
				currentSimileFile = currentSimile == null ? null : Path.GetFileName(open.FileName);

				// Update window based on results
				if (currentSimile == null)
				{
					windowControl.TextSimileFileName.Text = "No file loaded";
					CaptureButtonEnabled = false;
				}
				else
				{
					windowControl.TextSimileFileName.Text = currentSimileFile;
					CaptureButtonEnabled = true;
				}
			}
		}

		/// <summary>
		/// Starts a capture of the current project
		/// </summary>
		public void BeginCapture()
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			// Can't capture without a simile
			if (currentSimile == null)
				return;

			capture.BeginCapture(currentSimile);

			//// Just testing...
			//ConsoleSimile check = new ConsoleSimile();
			//check.AddOutput("Hello, World!");
			//for (int i = 0; i < 10; i++)
			//	check.AddOutput(i.ToString());
			//check.AddOutput("Enter your name: ", LineEndingType.SameLine);
			//check.AddInput("Chris");
			//check.AddOutput("Your name is Chris");

			//capture.BeginCapture(check);
		}

		/// <summary>
		/// Stops a capture in progress if there is one
		/// </summary>
		public void StopCapture()
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			capture.StopCapture();
		}


		/// <summary>
		/// Sets the status text
		/// </summary>
		/// <param name="text">New text, without the label "Status:"</param>
		public void SetStatus(string text)
		{
			windowControl.TextStatus.Text = text;
		}

		/// <summary>
		/// Clears the text of both rich text boxes (output and expected)
		/// </summary>
		public void ClearAllOutputText()
		{
			windowControl.ProgramOutput.Document.Blocks.Clear();
			windowControl.ExpectedOutput.Document.Blocks.Clear();
		}


		public void AddTextOutput(string text, SolidColorBrush color, SolidColorBrush backColor, FontStyle style, FontWeight weight, bool appendToPreviousLine)
			=> AddText(text, color, backColor, style, weight, appendToPreviousLine, windowControl.ProgramOutput);

		public void AddTextExpected(string text, SolidColorBrush color, SolidColorBrush backColor, FontStyle style, FontWeight weight, bool appendToPreviousLine)
			=> AddText(text, color, backColor, style, weight, appendToPreviousLine, windowControl.ExpectedOutput);

		/// <summary>
		/// Private helper for adding colored text to a particular text box
		/// </summary>
		private void AddText(string text, SolidColorBrush color, SolidColorBrush backColor, FontStyle style, FontWeight weight, bool appendToPreviousLine, RichTextBox textBox)
		{
			// Set up a text run with proper color
			Run run = new Run(text) { Foreground = color, Background = backColor, FontStyle = style, FontWeight = weight };

			// Are we appending to the previous line and is there one?
			if (appendToPreviousLine &&
				textBox.Document.Blocks.Count > 0 &&
				textBox.Document.Blocks.LastBlock is Paragraph p)
			{
				p.Inlines.Add(run);
			}
			else
			{
				// Not appending, or there is nothing to append to
				Paragraph newPara = new Paragraph() { Margin = new Thickness(0) };

				// Add the run to the paragraph, then add the paragraph to the output
				newPara.Inlines.Add(run);
				textBox.Document.Blocks.Add(newPara);
			}
		}
	}
}
