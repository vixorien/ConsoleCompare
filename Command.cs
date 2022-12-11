using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.ComponentModel.Design;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

using EnvDTE;
using System.Diagnostics;
using VSLangProj;
using System.IO;
using Microsoft.VisualStudio;
using EnvDTE80;

namespace ConsoleCompare
{
	/// <summary>
	/// Command handler
	/// </summary>
	internal sealed class Command
	{
		/// <summary>
		/// Command ID.
		/// </summary>
		public const int CommandId = 0x0100;

		/// <summary>
		/// Command menu group (command set GUID).
		/// </summary>
		public static readonly Guid CommandSet = new Guid("ffc8cf64-11d2-4e24-93e2-e18c12bf16fd");

		/// <summary>
		/// VS Package that provides this command, not null.
		/// </summary>
		private readonly AsyncPackage package;

		/// <summary>
		/// Initializes a new instance of the <see cref="Command"/> class.
		/// Adds our command handlers for menu (commands must exist in the command table file)
		/// </summary>
		/// <param name="package">Owner package, not null.</param>
		/// <param name="commandService">Command service to add command to, not null.</param>
		private Command(AsyncPackage package, OleMenuCommandService commandService)
		{
			this.package = package ?? throw new ArgumentNullException(nameof(package));
			commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

			var menuCommandID = new CommandID(CommandSet, CommandId);
			var menuItem = new MenuCommand(this.Execute, menuCommandID);
			commandService.AddCommand(menuItem);
		}

		/// <summary>
		/// Gets the instance of the command.
		/// </summary>
		public static Command Instance
		{
			get;
			private set;
		}

		/// <summary>
		/// Gets the service provider from the owner package.
		/// </summary>
		private Microsoft.VisualStudio.Shell.IAsyncServiceProvider ServiceProvider
		{
			get
			{
				return this.package;
			}
		}

		/// <summary>
		/// Initializes the singleton instance of the command.
		/// </summary>
		/// <param name="package">Owner package, not null.</param>
		public static async Task InitializeAsync(AsyncPackage package)
		{
			// Switch to the main thread - the call to AddCommand in Command's constructor requires
			// the UI thread.
			await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

			OleMenuCommandService commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
			Instance = new Command(package, commandService);
		}

		/// <summary>
		/// This function is the callback used to execute the command when the menu item is clicked.
		/// See the constructor to see how the menu item is associated with this function using
		/// OleMenuCommandService service and MenuCommand class.
		/// </summary>
		/// <param name="sender">Event sender.</param>
		/// <param name="e">Event args.</param>
		private void Execute(object sender, EventArgs e)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			// Open the window - all other interactions happen there
			OpenWindow();
		}


		// Opening the window programmatically
		// From: https://stackoverflow.com/a/31120230
		private ResultsWindow OpenWindow()
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			ResultsWindow window = (ResultsWindow)this.package.FindToolWindow(typeof(ResultsWindow), 0, true); // True means: create if not found. 0 means there is only 1 instance of this tool window
			if (window == null || window.Frame == null)
				throw new NotSupportedException("MyToolWindow not found");

			IVsWindowFrame windowFrame = (IVsWindowFrame)window.Frame;
			ErrorHandler.ThrowOnFailure(windowFrame.Show());

			return window;
		}

		// Opening the window programmatically if we don't have the package
		// - Unnecessary, but just for our reference!
		// From: https://stackoverflow.com/a/31120230
		private void OpenWindowThroughUIShell()
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			IVsUIShell vsUIShell = (IVsUIShell)Package.GetGlobalService(typeof(SVsUIShell));
			Guid guid = typeof(ResultsWindow).GUID;

			IVsWindowFrame windowFrame;

			// Attempt to find the window
			int result = vsUIShell.FindToolWindow((uint)__VSFINDTOOLWIN.FTW_fFindFirst, ref guid, out windowFrame);

			// If that didn't work, make it
			if(result != VSConstants.S_OK)
				result = vsUIShell.FindToolWindow((uint)__VSFINDTOOLWIN.FTW_fForceCreate, ref guid, out windowFrame);

			// As long as it's fine, show it
			if (result == VSConstants.S_OK)
				ErrorHandler.ThrowOnFailure(windowFrame.Show());
		}
	}
}
