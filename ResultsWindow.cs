using Microsoft.VisualStudio.Shell;
using System;
using System.Runtime.InteropServices;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows;
using System.Windows.Controls;

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
		}

		/// <summary>
		/// Starts a capture of the current project
		/// </summary>
		public void BeginCapture()
		{
			// Just testing...
			ConsoleSimile check = new ConsoleSimile();
			check.AddOutput("Hello, World!", true);
			for (int i = 0; i < 10; i++)
				check.AddOutput(i.ToString(), true);
			check.AddOutput("Enter your name: ", false);
			check.AddInput("Chris");
			check.AddOutput("Your name is Chris", true);

			capture.BeginCapture(check);
		}

		/// <summary>
		/// Sets the status text
		/// </summary>
		/// <param name="text">New text, without the label "Status:"</param>
		public void SetStatus(string text)
		{
			windowControl.TextStatus.Text = "Status: " + text;
		}

		/// <summary>
		/// Clears the text of both rich text boxes (output and expected)
		/// </summary>
		public void ClearAllText()
		{
			windowControl.ProgramOutput.Document.Blocks.Clear();
			windowControl.ExpectedOutput.Document.Blocks.Clear();
		}

		public void AddTextOutput(string text) => AddText(text, Brushes.White, FontStyles.Normal, windowControl.ProgramOutput);
		public void AddTextOutput(string text, FontStyle style) => AddText(text, Brushes.White, style, windowControl.ProgramOutput);
		public void AddTextOutput(string text, SolidColorBrush color) => AddText(text, color, FontStyles.Normal, windowControl.ProgramOutput);
		public void AddTextOutput(string text, FontStyle style, SolidColorBrush color) => AddText(text, color, style, windowControl.ProgramOutput);

		public void AddTextExpected(string text) => AddText(text, Brushes.White, FontStyles.Normal, windowControl.ExpectedOutput);
		public void AddTextExpected(string text, FontStyle style) => AddText(text, Brushes.White, style, windowControl.ExpectedOutput);
		public void AddTextExpected(string text, SolidColorBrush color) => AddText(text, color, FontStyles.Normal, windowControl.ExpectedOutput);
		public void AddTextExpected(string text, FontStyle style, SolidColorBrush color) => AddText(text, color, style, windowControl.ExpectedOutput);

		/// <summary>
		/// Private helper for adding colored text to a particular text box
		/// </summary>
		private void AddText(string text, SolidColorBrush color, FontStyle style, RichTextBox textBox)
		{
			// Set up a text run with proper color
			Run run = new Run(text) { Foreground = color, FontStyle = style };

			// Create a paragraph for the run with no margin
			Paragraph p = new Paragraph() { Margin = new Thickness(0) };

			// Add the run to the paragraph, then add the paragraph to the output
			p.Inlines.Add(run);
			textBox.Document.Blocks.Add(p);
		}
	}
}
