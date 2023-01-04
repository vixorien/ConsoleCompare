using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.Win32;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;

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
		// Color references: https://learn.microsoft.com/en-us/dotnet/media/art-color-table.png
		private static ImageMoniker ComparisonOutputMatchIcon = KnownMonikers.StatusOK;
		private static ImageMoniker ComparisonOutputMismatchIcon = KnownMonikers.StatusError;
		private static ImageMoniker ComparisonExpectedMatchIcon = KnownMonikers.StatusOKNoColor;
		private static ImageMoniker ComparisonExpectedMismatchIcon = KnownMonikers.StatusErrorOutline;
		private static SolidColorBrush BackgroundColor = Brushes.Black;
		private static SolidColorBrush ExpectedOutputColor = Brushes.WhiteSmoke;
		private static SolidColorBrush MatchingOutputColor = Brushes.Green;
		private static SolidColorBrush NonmatchingOutputColor = Brushes.Firebrick;
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
			this.Caption = "Console Compare";

			// This is the user control hosted by the tool window; Note that, even if this class implements IDisposable,
			// we are not calling Dispose on this object. This is because ToolWindowPane calls Dispose on
			// the object returned by the Content property.
			this.windowControl = new ResultsWindowControl(this);
			this.Content = windowControl; // Casts to object

			// Create the capture manager with a reference to this window
			capture = new CaptureManager(this);
			SetStatus("Extension ready. Load simile file to begin.", KnownMonikers.StatusInformation);

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
				// Clear the text
				ClearAllOutputText();

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
					SetStatus("Invalid simile file.", KnownMonikers.StatusError);
				}
				else
				{
					windowControl.TextSimileFileName.Text = filename;
					CaptureButtonEnabled = true;
					SetStatus("Simile file loaded. Press run button to compare output.", KnownMonikers.StatusInformation);
				}
			}
		}

		/// <summary>
		/// Starts a capture of the current project
		/// </summary>
		public void BeginCapture()
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			// Scan for comments and report the results
			CommentChecker.ScanForComments(this);

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
		/// Sets the status text and icon
		/// </summary>
		/// <param name="text">Text to display</param>
		/// <param name="icon">Icon to show</param>
		public void SetStatus(string text, ImageMoniker icon)
		{
			windowControl.TextStatus.Text = text;
			windowControl.StatusIcon.Moniker = icon;
		}

		/// <summary>
		/// Sets the status text without changing the icon
		/// </summary>
		/// <param name="text">Text to display</param>
		public void SetStatusNoIconChange(string text)
		{
			windowControl.TextStatus.Text = text;
		}

		/// <summary>
		/// Starts the animation for the "run" status
		/// </summary>
		public void BeginRunStatusAnimation()
		{
			//ImageMoniker[] animationFrames =
			//{
			//	KnownMonikers.LevelOne,
			//	KnownMonikers.LevelTwo,
			//	KnownMonikers.LevelThree,
			//	KnownMonikers.LevelFour
			//};

			ImageMoniker[] animationFrames =
			{
				KnownMonikers.FourRows,
				KnownMonikers.FirstOfFourRows,
				KnownMonikers.SecondOfFourRows,
				KnownMonikers.ThirdOfFourRows,
				KnownMonikers.FourthOfFourRows
			};

			// Start on first frame
			windowControl.StatusIcon.Moniker = animationFrames[0];

			ObjectAnimationUsingKeyFrames frames = new ObjectAnimationUsingKeyFrames();

			// Proceed to other frames
			for (int i = 1; i < animationFrames.Length; i++)
				frames.KeyFrames.Add(new DiscreteObjectKeyFrame(animationFrames[i]));
			frames.KeyFrames.Add(new DiscreteObjectKeyFrame(animationFrames[0])); // Never displays the last (presumably since there's no interpolation?)

			frames.Duration = new Duration(TimeSpan.FromSeconds(4));
			frames.RepeatBehavior = RepeatBehavior.Forever;
			

			windowControl.StatusIcon.BeginAnimation(CrispImage.MonikerProperty, frames);
		}

		/// <summary>
		/// Stops the animation for the "run" status
		/// </summary>
		public void EndRunStatusAnimation()
		{
			windowControl.StatusIcon.BeginAnimation(CrispImage.MonikerProperty, null);
		}


		/// <summary>
		/// Sets the comment-specific status text and icon
		/// </summary>
		/// <param name="text">Text to display</param>
		/// <param name="icon">Icon to show</param>
		/// <param name="commentDetails">Extra details to be displayed on click</param>
		/// <param name="animateIcon">Should the icon be animated for a short while?</param>
		public void SetCommentStatus(string text, ImageMoniker icon, string commentDetails = null, bool animateIcon = true)
		{
			windowControl.TextComments.Text = text + (string.IsNullOrEmpty(commentDetails) ? "" : " (Click for details)");
			windowControl.TextComments.Tag = commentDetails;
			windowControl.CommentIcon.Moniker = icon;

			// TODO: Create this once at start up
			ObjectAnimationUsingKeyFrames frames = new ObjectAnimationUsingKeyFrames();
			frames.KeyFrames.Add(new DiscreteObjectKeyFrame(KnownMonikers.StatusInformationOutline));
			frames.KeyFrames.Add(new DiscreteObjectKeyFrame(icon));
			frames.Duration = new Duration(TimeSpan.FromSeconds(0.5));
			frames.RepeatBehavior = new RepeatBehavior(3);

			windowControl.CommentIcon.BeginAnimation(CrispImage.MonikerProperty, frames);
		}

		/// <summary>
		/// Shows comment details as a basic message box (if necessary)
		/// </summary>
		public void ShowCommentDetailsPopup()
		{
			string details = windowControl.TextComments.Tag as string;
			if (string.IsNullOrEmpty(details))
				return;

			MessageBox.Show(
				details,
				"Comment Details",
				MessageBoxButton.OK,
				MessageBoxImage.Information);
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
