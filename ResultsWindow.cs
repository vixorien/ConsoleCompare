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
using Microsoft.VisualStudio.Imaging.Interop;

namespace ConsoleCompare
{
	/// <summary>
	/// The IO type of text for the results window
	/// </summary>
	public enum ResultsTextType
	{
		Output,
		Input
	}

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
		// Output details
		private static ImageMoniker ComparisonOutputMatchIcon = KnownMonikers.StatusOK;
		private static ImageMoniker ComparisonOutputMismatchIcon = KnownMonikers.StatusError;
		private static ImageMoniker ComparisonExpectedMatchIcon = KnownMonikers.StatusOKNoColor;
		private static ImageMoniker ComparisonExpectedMismatchIcon = KnownMonikers.StatusErrorOutline;
		private static SolidColorBrush BackgroundColor = Brushes.Black;
		private static SolidColorBrush ExpectedOutputColor = Brushes.White;
		private static SolidColorBrush MatchingOutputColor = Brushes.Green;
		private static SolidColorBrush NonmatchingOutputColor = Brushes.OrangeRed;
		private static FontStyle OutputFontStyle = FontStyles.Normal;
		private static FontStyle InputFontStyle = FontStyles.Italic;
		private static FontWeight OutputFontWeight = FontWeights.Normal;
		private static FontWeight InputFontWeight = FontWeights.Bold;
		private static bool InvertInputColors = false;

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
				// Parse and check the results
				string filename = Path.GetFileName(open.FileName);

				try
				{
					currentSimile = null; // Reset first
					currentSimile = SimileParser.ParseFromFile(open.FileName);
				}
				catch (SimileParseException e)
				{
					// Create a detailed error message including the line that failed
					string errorMessage = 
						$"Parse of '{filename}' failed\n\n" +
						e.Message + "\n\n" +
						$"Line in question:\n'{e.LineText}'";

					MessageBox.Show(
						errorMessage,
						"Error Parsing File",
						MessageBoxButton.OK,
						MessageBoxImage.Error);
				}
				catch (Exception e)
				{
					// Other misc error with the file
					MessageBox.Show(
						$"Error opening file '{filename}': {e.Message}",
						"Error Opening File",
						MessageBoxButton.OK,
						MessageBoxImage.Error);
				}

				// Update window based on results
				if (currentSimile == null)
				{
					windowControl.TextSimileFileName.Text = "Load Simile File";
					CaptureButtonEnabled = false;
				}
				else
				{
					windowControl.TextSimileFileName.Text = filename;
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

		/// <summary>
		/// Adds text to the program output text box
		/// </summary>
		/// <param name="text">The text to add</param>
		/// <param name="textType">The type of text, either input or output</param>
		/// <param name="appendToPreviousLine">Is this appended to the previous line?</param>
		/// <param name="match">Is this text considered a match?</param>
		public void AddTextOutput(string text, ResultsTextType textType, bool appendToPreviousLine, bool match)
		{
			// Display options for adding text
			ImageMoniker icon = match ? ComparisonOutputMatchIcon : ComparisonOutputMismatchIcon;
			SolidColorBrush backColor;
			SolidColorBrush foreColor;
			FontStyle style;
			FontWeight weight;

			// Check the line type
			switch (textType)
			{
				default:
				case ResultsTextType.Output:

					// Static background color, foreground depends on match
					backColor = BackgroundColor;
					foreColor = match ? MatchingOutputColor : NonmatchingOutputColor;
					style = OutputFontStyle;
					weight = OutputFontWeight;
					break;

				case ResultsTextType.Input:

					// Depends only on inversion option, as we always assume input matches
					backColor = InvertInputColors ? MatchingOutputColor : BackgroundColor;
					foreColor = InvertInputColors ? BackgroundColor : MatchingOutputColor;
					style = InputFontStyle;
					weight = InputFontWeight;
					break;
			}

			// Pass final values to the helper
			AddText(
				text, 
				foreColor, 
				backColor, 
				style, 
				weight, 
				appendToPreviousLine, 
				windowControl.ProgramOutput, 
				icon);
		}

		/// <summary>
		/// Adds text to the expected output text box
		/// </summary>
		/// <param name="text">The text to add</param>
		/// <param name="textType">The type of text, either input or output</param>
		/// <param name="appendToPreviousLine">Is this appended to the previous line?</param>
		/// <param name="match">Is this text considered a match?</param>
		public void AddTextExpected(string text, ResultsTextType textType, bool appendToPreviousLine, bool match)
		{
			// Display options for adding text
			ImageMoniker icon = match ? ComparisonExpectedMatchIcon : ComparisonExpectedMismatchIcon;
			SolidColorBrush backColor;
			SolidColorBrush foreColor;
			FontStyle style;
			FontWeight weight;

			// Check the line type
			switch (textType)
			{
				default:
				case ResultsTextType.Output:

					// Static background color, foreground depends on match
					backColor = BackgroundColor;
					foreColor = ExpectedOutputColor;
					style = OutputFontStyle;
					weight = OutputFontWeight;
					break;

				case ResultsTextType.Input:

					// Depends only on inversion option, as we always assume input matches
					backColor = InvertInputColors ? ExpectedOutputColor : BackgroundColor;
					foreColor = InvertInputColors ? BackgroundColor : ExpectedOutputColor;
					style = InputFontStyle;
					weight = InputFontWeight;
					break;
			}

			// Pass final values to the helper
			AddText(
				text, 
				foreColor, 
				backColor, 
				style, 
				weight, 
				appendToPreviousLine, 
				windowControl.ExpectedOutput, 
				icon);
		}

		/// <summary>
		/// Private helper for adding colored text to a particular text box
		/// </summary>
		/// <param name="text">The text to add</param>
		/// <param name="color">The color of the text</param>
		/// <param name="backColor">The background color of the text</param>
		/// <param name="style">The font style (italics)</param>
		/// <param name="weight">The font weight (bold)</param>
		/// <param name="appendToPreviousLine">Is this appended to the previous line?</param>
		/// <param name="textBox">The text box to place the text in</param>
		/// <param name="icon">The icon to prepend to the line, if any</param>
		private void AddText(string text, SolidColorBrush color, SolidColorBrush backColor, FontStyle style, FontWeight weight, bool appendToPreviousLine, RichTextBox textBox, ImageMoniker? icon)
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

				// Do we need to toss a match icon at the front of the line?
				if (icon.HasValue)
				{
					// Create the image and add to the paragraph
					CrispImage ci = new CrispImage();
					ci.Moniker = icon.Value;
					newPara.Inlines.Add(ci);

					// Add a space to the run, too
					run.Text = " " + run.Text;
				}

				// Add the run to the paragraph, then add the paragraph to the output
				newPara.Inlines.Add(run);
				textBox.Document.Blocks.Add(newPara);
			}
		}
	}
}
