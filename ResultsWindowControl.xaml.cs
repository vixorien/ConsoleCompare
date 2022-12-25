using Microsoft.VisualStudio.Shell;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;

namespace ConsoleCompare
{
	/// <summary>
	/// Interaction logic for ResultsWindowControl.
	/// </summary>
	public partial class ResultsWindowControl : UserControl
	{
		private ResultsWindow window;

		/// <summary>
		/// Initializes a new instance of the <see cref="ResultsWindowControl"/> class.
		/// </summary>
		public ResultsWindowControl(ResultsWindow window)
		{
			this.window = window;
			this.InitializeComponent();

			// Set up widths of rich text boxes
			this.ExpectedOutput.Document.PageWidth = 1000;
			this.ProgramOutput.Document.PageWidth = 1000;
		}

		/// <summary>
		/// Begins a run of the application and captures the output, comparing
		/// it to a set of predefined output
		/// </summary>
		private void ButtonCapture_Click(object sender, RoutedEventArgs e)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			window.BeginCapture();
		}

		/// <summary>
		/// Stops a capture in progress, if one exists
		/// </summary>
		private void ButtonStop_Click(object sender, RoutedEventArgs e)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			window.StopCapture();
		}

		/// <summary>
		/// Loads a simile file
		/// </summary>
		private void ButtonLoadSimile_Click(object sender, RoutedEventArgs e)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			window.LoadSimileUsingFileDialog();
		}

		/// <summary>
		/// Ensures the two text boxes remain sync'd as they scroll
		/// </summary>
		private void SyncScrollChanged(object sender, ScrollChangedEventArgs e)
		{
			RichTextBox toSync = (sender == this.ExpectedOutput) ? this.ProgramOutput : this.ExpectedOutput;

			toSync.ScrollToVerticalOffset(e.VerticalOffset);
			toSync.ScrollToHorizontalOffset(e.HorizontalOffset);
		}
    }
}