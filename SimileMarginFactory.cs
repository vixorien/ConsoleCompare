﻿using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

namespace ConsoleCompare
{
	/// <summary>
	/// Export a <see cref="IWpfTextViewMarginProvider"/>, which returns an instance of the margin for the editor to use.
	/// </summary>
	[Export(typeof(IWpfTextViewMarginProvider))]
	[Name(SimileMargin.MarginName)]
	[Order(After = PredefinedMarginNames.Top)]  // Ensure that the margin occurs below the horizontal scrollbar
	[MarginContainer(PredefinedMarginNames.Top)]             // Set the container to the bottom of the editor window
	[ContentType("simile")]                                       // Show this margin for all text-based types
	[TextViewRole(PredefinedTextViewRoles.Interactive)]
	internal sealed class SimileMarginFactory : IWpfTextViewMarginProvider
	{
		#region IWpfTextViewMarginProvider

		/// <summary>
		/// Creates an <see cref="IWpfTextViewMargin"/> for the given <see cref="IWpfTextViewHost"/>.
		/// </summary>
		/// <param name="wpfTextViewHost">The <see cref="IWpfTextViewHost"/> for which to create the <see cref="IWpfTextViewMargin"/>.</param>
		/// <param name="marginContainer">The margin that will contain the newly-created margin.</param>
		/// <returns>The <see cref="IWpfTextViewMargin"/>.
		/// The value may be null if this <see cref="IWpfTextViewMarginProvider"/> does not participate for this context.
		/// </returns>
		public IWpfTextViewMargin CreateMargin(IWpfTextViewHost wpfTextViewHost, IWpfTextViewMargin marginContainer)
		{
			return new SimileMargin(wpfTextViewHost.TextView);
		}

		#endregion
	}
}
