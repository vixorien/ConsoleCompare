using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ConsoleCompare
{
	/// <summary>
	/// Margin's canvas and visual definition including both size and content
	/// </summary>
	internal class SimileMargin : Canvas, IWpfTextViewMargin
	{
		private const int sizeOpen = 200;
		private const int sizeCollapsed = 20;

		private bool marginOpen;

		/// <summary>
		/// Margin name.
		/// </summary>
		public const string MarginName = "SimileMargin";

		/// <summary>
		/// A value indicating whether the object is disposed.
		/// </summary>
		private bool isDisposed;

		/// <summary>
		/// Initializes a new instance of the <see cref="SimileMargin"/> class for a given <paramref name="textView"/>.
		/// </summary>
		/// <param name="textView">The <see cref="IWpfTextView"/> to attach the margin to.</param>
		public SimileMargin(IWpfTextView textView)
		{
			marginOpen = true;
			this.Height = sizeOpen; // start open
			this.ClipToBounds = true;
			this.Background = new SolidColorBrush(Colors.Black);

			// Set up events
			this.MouseDown += SimileMargin_MouseDown;

			// Add a green colored label that says "Hello SimileMargin"
			var label = new Label
			{
				Background = new SolidColorBrush(Colors.Black),
				Foreground = new SolidColorBrush(Colors.WhiteSmoke),
				Content = "Hello SimileMargin",
				FontFamily = new FontFamily("Cascadia Code")
			};

			
			this.Children.Add(label);
		}

		/// <summary>
		/// Handles a mouse click in the margin
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void SimileMargin_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			if (marginOpen)
			{
				// Collapse
				marginOpen = false;
				this.Height = sizeCollapsed;
			}
			else
			{
				// Open back up
				marginOpen = true;
				this.Height = sizeOpen;
			}
		}

		#region IWpfTextViewMargin

		/// <summary>
		/// Gets the <see cref="Sytem.Windows.FrameworkElement"/> that implements the visual representation of the margin.
		/// </summary>
		/// <exception cref="ObjectDisposedException">The margin is disposed.</exception>
		public FrameworkElement VisualElement
		{
			// Since this margin implements Canvas, this is the object which renders
			// the margin.
			get
			{
				this.ThrowIfDisposed();
				return this;
			}
		}

		#endregion

		#region ITextViewMargin

		/// <summary>
		/// Gets the size of the margin.
		/// </summary>
		/// <remarks>
		/// For a horizontal margin this is the height of the margin,
		/// since the width will be determined by the <see cref="ITextView"/>.
		/// For a vertical margin this is the width of the margin,
		/// since the height will be determined by the <see cref="ITextView"/>.
		/// </remarks>
		/// <exception cref="ObjectDisposedException">The margin is disposed.</exception>
		public double MarginSize
		{
			get
			{
				this.ThrowIfDisposed();

				// Since this is a horizontal margin, its width will be bound to the width of the text view.
				// Therefore, its size is its height.
				return this.ActualHeight;
			}
		}

		/// <summary>
		/// Gets a value indicating whether the margin is enabled.
		/// </summary>
		/// <exception cref="ObjectDisposedException">The margin is disposed.</exception>
		public bool Enabled
		{
			get
			{
				this.ThrowIfDisposed();

				// The margin should always be enabled
				return true;
			}
		}

		/// <summary>
		/// Gets the <see cref="ITextViewMargin"/> with the given <paramref name="marginName"/> or null if no match is found
		/// </summary>
		/// <param name="marginName">The name of the <see cref="ITextViewMargin"/></param>
		/// <returns>The <see cref="ITextViewMargin"/> named <paramref name="marginName"/>, or null if no match is found.</returns>
		/// <remarks>
		/// A margin returns itself if it is passed its own name. If the name does not match and it is a container margin, it
		/// forwards the call to its children. Margin name comparisons are case-insensitive.
		/// </remarks>
		/// <exception cref="ArgumentNullException"><paramref name="marginName"/> is null.</exception>
		public ITextViewMargin GetTextViewMargin(string marginName)
		{
			return string.Equals(marginName, SimileMargin.MarginName, StringComparison.OrdinalIgnoreCase) ? this : null;
		}

		/// <summary>
		/// Disposes an instance of <see cref="SimileMargin"/> class.
		/// </summary>
		public void Dispose()
		{
			if (!this.isDisposed)
			{
				GC.SuppressFinalize(this);
				this.isDisposed = true;
			}
		}

		#endregion

		/// <summary>
		/// Checks and throws <see cref="ObjectDisposedException"/> if the object is disposed.
		/// </summary>
		private void ThrowIfDisposed()
		{
			if (this.isDisposed)
			{
				throw new ObjectDisposedException(MarginName);
			}
		}
	}
}
