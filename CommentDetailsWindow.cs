using Microsoft.VisualStudio.Shell;
using System;
using System.Runtime.InteropServices;

namespace ConsoleCompare
{
	[Guid("d0925d87-0fdd-4a9e-bf73-ed0e4082aadf")]
	internal class CommentDetailsWindow : ToolWindowPane
	{
		/// <summary>
		/// The window control that has window elements
		/// </summary>
		private CommentDetailsControl windowControl;

		/// <summary>
		/// Creates a new comment details window
		/// </summary>
		public CommentDetailsWindow() : base(null)
		{
			this.windowControl = new CommentDetailsControl();
			this.Content = windowControl;
		}

		/// <summary>
		/// Sets the text of the comment details text box
		/// </summary>
		/// <param name="commentDetails">Details to display in the window</param>
		public void SetCommentDetails(string commentDetails)
		{
			windowControl.TextCommentDetails.Text = commentDetails;
		}

		/// <summary>
		/// Static helper to open the comment details window
		/// </summary>
		/// <param name="commentDetails">Comment details to display</param>
		public static void OpenWindow(string commentDetails)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			// Grab the overall package for our extension
			AsyncPackage package = Command.Instance.ServiceProviderPackage;
			if (package == null)
				return;

			// Perform an async show/create and set the details if possible
			_ = package.JoinableTaskFactory.RunAsync(async delegate
			{
				ToolWindowPane window = await package.ShowToolWindowAsync(typeof(CommentDetailsWindow), 0, true, package.DisposalToken);
				if ((null == window) || (null == window.Frame))
				{
					throw new NotSupportedException("Cannot create comment details window");
				}

				// Window create/show was successful, so update details
				(window as CommentDetailsWindow)?.SetCommentDetails(commentDetails);
			});
		}
	}
}
