﻿using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ConsoleCompare
{
	/// <summary>
	/// An extra UI element that displays simile file details
	/// Note: Some of this was auto-generated from a template!
	/// </summary>
	internal class SimileMargin : Canvas, IWpfTextViewMargin
	{
		// Constants for display sizing
		private const int sizeOpen = 200;
		private const double fontSizeAdjust = 0.8;

		// Content details
		private bool marginOpen;
		private Label contentCollapsed;
		private ScrollViewer contentOpen;
		private IWpfTextView textView;

		// Required for Visual Studio margin details (part of the template - do not remove)
		public const string MarginName = "SimileMargin";

		// Required for IDisposable implementation
		private bool isDisposed;


		/// <summary>
		/// Initializes a new instance of the <see cref="SimileMargin"/> class for a given <paramref name="textView"/>.
		/// </summary>
		/// <param name="textView">The <see cref="IWpfTextView"/> to attach the margin to.</param>
		public SimileMargin(IWpfTextView textView)
		{
			// Set up overall margin details
			this.ClipToBounds = true;
			this.Background = new SolidColorBrush(Colors.Black);
			
			// Save text view ref
			this.textView = textView;

			// Set up events
			this.MouseUp += SimileMargin_MouseUp;
			this.SizeChanged += SimileMargin_SizeChanged;
			textView.ZoomLevelChanged += TextView_ZoomLevelChanged;

			// Lable for when the margin is collapsed
			contentCollapsed = new Label
			{
				Background = new SolidColorBrush(Colors.Black),
				Foreground = new SolidColorBrush(Colors.WhiteSmoke),
				FontFamily = new FontFamily("Cascadia Code"),
				Content = "Click for simile file syntax..."
			};

			// Content for when the margin is open
			contentOpen = new ScrollViewer
			{
				Width = this.Width,
				Height = this.Height,
				Padding = contentCollapsed.Padding,
				VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
				HorizontalScrollBarVisibility = ScrollBarVisibility.Visible,
				Background = new SolidColorBrush(Colors.Black),
				Foreground = new SolidColorBrush(Colors.WhiteSmoke),
				FontFamily = new FontFamily("Cascadia Code"),
				Content = SimileParser.SimileSyntaxDetails
			};

			// Add content to margin
			this.Children.Add(contentCollapsed);
			this.Children.Add(contentOpen);

			// Set the initial state to closed (after creating labels!) and adjust font sizes
			SetMarginState(false);
			UpdateFontSizes();
		}

		/// <summary>
		/// Adjusts font sizes when the text view zoom level changes
		/// </summary>
		private void TextView_ZoomLevelChanged(object sender, ZoomLevelChangedEventArgs e)
		{
			UpdateFontSizes();
		}

		/// <summary>
		/// Handles a resize of content when the margin size is changed
		/// </summary>
		private void SimileMargin_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			// Update the scroll viewer to the margin's actual size
			if (e.WidthChanged) contentOpen.Width = e.NewSize.Width;
			if (e.HeightChanged) contentOpen.Height = e.NewSize.Height;
		}

		/// <summary>
		/// Sets the margin's collapsed/open state and other corresponding UI elements
		/// </summary>
		/// <param name="open">Should the margin be fully open (true) or collapsed (false)</param>
		private void SetMarginState(bool open)
		{
			// Save new state and update accordingly
			marginOpen = open;
			this.Height = marginOpen ? sizeOpen : CalculateCollapsedMarginHeight();

			// Flip flop the content visibility, too
			if (marginOpen)
			{
				contentOpen.Visibility = Visibility.Visible;
				contentCollapsed.Visibility = Visibility.Collapsed;
			}
			else
			{
				contentOpen.Visibility = Visibility.Collapsed;
				contentCollapsed.Visibility = Visibility.Visible;
			}
		}

		/// <summary>
		/// Updates the font sizes of the margin contents and, if necessary,
		/// adjusts the collapsed margin's height
		/// </summary>
		private void UpdateFontSizes()
		{
			// Set the font sizes based on the text view
			double zoomAdjust = textView.ZoomLevel / 100.0;
			contentOpen.FontSize = textView.LineHeight * fontSizeAdjust * zoomAdjust;
			contentCollapsed.FontSize = textView.LineHeight * fontSizeAdjust * zoomAdjust;

			// Adjust collapsed height, too
			if (!marginOpen)
			{
				this.Height = CalculateCollapsedMarginHeight();
			}
		}

		/// <summary>
		/// Calculates the size of the collapsed margin based on current zoom level
		/// </summary>
		/// <returns>The desired height of the collapsed margin</returns>
		private double CalculateCollapsedMarginHeight()
		{
			double zoomAdjust = textView.ZoomLevel / 100.0;
			return 
				textView.LineHeight * zoomAdjust + 
				contentCollapsed.Padding.Top + 
				contentCollapsed.Padding.Bottom;
		}

		/// <summary>
		/// Handles a mouse click in the margin
		/// </summary>
		private void SimileMargin_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			// Flip the state
			SetMarginState(!marginOpen);
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
