using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.TableControl;
using Microsoft.VisualStudio.Shell.TableManager;
using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleCompare
{
	/// <summary>
	/// The class that acts as a source of data for a table in the visual studio UI
	/// Microsoft ref: https://github.com/microsoft/VSSDK-Extensibility-Samples/tree/master/ErrorList
	/// Ref: https://github.com/madskristensen/WebAccessibilityChecker/tree/master/src/ErrorList
	/// </summary>
	internal class SimileErrorSource : ITableDataSource
	{
		// We need to handle potentially multiple "sinks" of data for the table
		// Note: There is usually just a single one of these (the error list in VS),
		//       but another extension may also subscribe so we need to handle that, too
		private List<SimileErrorSinkManager> sinkManagers;

		// Tracks errors on live buffers
		// - key: a buffer that is active (live)
		// - value: a list of error snapshots, one per line of text in the buffer (or null if no errors on that line)
		private Dictionary<ITextBuffer, List<SimileErrorSnapshot>> liveBufferErrors;

		// The provider that gives us access to the table
		// Note: the import tag means VS will populate this property for us!
		[Import]
		private ITableManagerProvider TableManagerProvider { get; set; }


		#region Singleton
		// The one and only instance of this class
		private static SimileErrorSource instance;

		/// <summary>
		/// Gets the single instance of this class, and instantiates 
		/// one if it doesn't exist yet
		/// </summary>
		public static SimileErrorSource Instance
		{
			get => instance ?? (instance = new SimileErrorSource());
		}
		#endregion

		/// <summary>
		/// Private constructor (due to singleton) to set up the error source
		/// </summary>
		private SimileErrorSource()
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			// Set up our data structures
			sinkManagers = new List<SimileErrorSinkManager>();
			liveBufferErrors = new Dictionary<ITextBuffer, List<SimileErrorSnapshot>>();

			// Ensure our imports are complete before moving on
			IComponentModel compositionService = ServiceProvider.GlobalProvider.GetService(typeof(SComponentModel)) as IComponentModel;
			compositionService?.DefaultCompositionService.SatisfyImportsOnce(this);

			// Set this class up as a source for the error table and define
			// which columns we're actually able to provide
			ITableManager table = TableManagerProvider.GetTableManager(StandardTables.ErrorsTable);
			table.AddSource(
				this,
				StandardTableColumnDefinitions.Line,
				StandardTableColumnDefinitions.Text,
				StandardTableColumnDefinitions.Column,
				StandardTableColumnDefinitions.DocumentName,
				StandardTableColumnDefinitions.ErrorSeverity);
		}

		/// <summary>
		/// Clears the error snapshot for the given line in the given text buffer
		/// </summary>
		/// <param name="buffer">The text buffer to which this line belongs</param>
		/// <param name="lineNumber">The line number in the buffer to clear</param>
		public void ClearErrorSnapshot(ITextBuffer buffer, int lineNumber)
		{
			// If this buffer doesn't exist, do nothing
			if (!liveBufferErrors.TryGetValue(buffer, out List<SimileErrorSnapshot> errorList) || 
				lineNumber >= errorList.Count)
				return;

			// As long as this line exists, null it out
			errorList[lineNumber] = null;
		}

		/// <summary>
		/// Adds an error snapshot for the given line in the given text buffer
		/// </summary>
		/// <param name="buffer">The text buffer this error is associated with</param>
		/// <param name="lineNumber">The line number in the buffer for this error</param>
		/// <param name="snapshot">The error snapshot, containing all errors for the given line</param>
		public void AddErrorSnapshot(ITextBuffer buffer, int lineNumber, SimileErrorSnapshot snapshot)
		{
			// If this buffer doesn't exist, do nothing
			if (!liveBufferErrors.TryGetValue(buffer, out List<SimileErrorSnapshot> errorList))
				return;

			// Buffer exists, so put the snapshot into the list in the proper location
			// A little hacky, but we need one element per line and the number of lines is variable
			while (errorList.Count <= lineNumber)
				errorList.Add(null);

			// Replace the given element with the new snapshot
			errorList[lineNumber] = snapshot;

			// Note: We're specifically NOT updating sinks here - that
			// will be done as a response to the buffer changed event from
			// the text view listener, which will fire PushAllErrorsToSinks()
		}

		/// <summary>
		/// Removes entries from the list of errors if the number of lines
		/// in the document has gone down (so "phantom" errors from old
		/// lines go away)
		/// </summary>
		/// <param name="buffer">The buffer to verify</param>
		public void VerifyBufferLength(ITextBuffer buffer)
		{
			// If this buffer doesn't exist, do nothing
			if (!liveBufferErrors.TryGetValue(buffer, out List<SimileErrorSnapshot> errorList))
				return;

			// If the actual line count is less than what we've been tracking,
			// then remove all excess line elements from the list
			int lineCount = buffer.CurrentSnapshot.LineCount;
			if (lineCount < errorList.Count)
				errorList.RemoveRange(lineCount, errorList.Count - lineCount);
		}

		/// <summary>
		/// Clears and re-sends all errors to each sink
		/// </summary>
		public void RefreshAllErrorsInSinks()
		{
			// Clear the sinks and push new errors
			foreach (SimileErrorSinkManager sink in sinkManagers)
			{
				// Clear all snapshots from the list
				sink.ClearAllSnapshots();

				// Toss all snapshots for each live buffer to this sink
				foreach (List<SimileErrorSnapshot> list in liveBufferErrors.Values)
				{
					sink.AddErrorSnapshots(list);
				}
			}
		}

		/// <summary>
		/// Registers a buffer as live so we can track its errors
		/// </summary>
		/// <param name="buffer">The buffer to register</param>
		public void RegisterLiveBuffer(ITextBuffer buffer)
		{
			if (!liveBufferErrors.ContainsKey(buffer))
				liveBufferErrors.Add(buffer, new List<SimileErrorSnapshot>());
		}

		/// <summary>
		/// Removes a buffer from our list of live buffers
		/// </summary>
		/// <param name="buffer">The buffer to remove</param>
		public void RemoveLiveBuffer(ITextBuffer buffer)
		{
			liveBufferErrors.Remove(buffer);
		}


		#region Error Sink Management

		/// <summary>
		/// Adds a sink manager to our list
		/// </summary>
		/// <param name="sinkManager">The sink manager to add</param>
		public void AddSinkManager(SimileErrorSinkManager sinkManager)
		{
			sinkManagers.Add(sinkManager);
		}

		/// <summary>
		/// Removes a sink manager from our list
		/// </summary>
		/// <param name="sinkManager">The sink manager to add</param>
		public void RemoveSinkManager(SimileErrorSinkManager sinkManager)
		{
			sinkManagers.Remove(sinkManager);
		}

		#endregion

		#region ITableDataSource

		/// <summary>
		/// Gets the type of table data we provide
		/// </summary>
		public string SourceTypeIdentifier => StandardTableDataSources.ErrorTableDataSource;

		/// <summary>
		/// Gets the ID of this data source
		/// </summary>
		public string Identifier => "Console Compare";

		/// <summary>
		/// Gets the friendly display name of this data source
		/// </summary>
		public string DisplayName => "Console Compare";

		/// <summary>
		/// Allows another object to subscribe to our list of errors
		/// </summary>
		/// <param name="sink">The sink that wants a list of errors</param>
		/// <returns>A manager for this sink</returns>
		public IDisposable Subscribe(ITableDataSink sink)
		{
			return new SimileErrorSinkManager(this, sink);
		}

		#endregion
	}
}
