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
		}


		private void ButtonCapture_Click(object sender, RoutedEventArgs e)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			window.BeginCapture();
		}
	}
}