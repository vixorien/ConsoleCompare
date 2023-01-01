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
	internal class SimileErrorSource : ITableDataSource
	{
		private List<SimileErrorSinkManager> sinkManagers;

		private Dictionary<ITextBuffer, List<SimileErrorSnapshot>> liveBufferErrors;

		[Import]
		private ITableManagerProvider TableManagerProvider { get; set; }

		#region Singleton
		private static SimileErrorSource instance;
		public static SimileErrorSource Instance
		{
			get => instance == null ? instance = new SimileErrorSource() : instance;
		}
		#endregion

		/// <summary>
		/// Private constructor (due to singleton) to set up the error source
		/// </summary>
		private SimileErrorSource()
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			sinkManagers = new List<SimileErrorSinkManager>();
			liveBufferErrors = new Dictionary<ITextBuffer, List<SimileErrorSnapshot>>();

			// Microsoft ref: https://github.com/microsoft/VSSDK-Extensibility-Samples/tree/master/ErrorList
			// Ref: https://github.com/madskristensen/WebAccessibilityChecker/tree/master/src/ErrorList

			// Ensure our imports are complete before moving on
			IComponentModel compositionService = ServiceProvider.GlobalProvider.GetService(typeof(SComponentModel)) as IComponentModel;
			compositionService?.DefaultCompositionService.SatisfyImportsOnce(this);

			// Set this class up as a source for the error table
			ITableManager table = TableManagerProvider.GetTableManager(StandardTables.ErrorsTable);
			table.AddSource(
				this,
				StandardTableColumnDefinitions.Line,
				StandardTableColumnDefinitions.Text,
				StandardTableColumnDefinitions.Column,
				StandardTableColumnDefinitions.DocumentName);
		}

		/// <summary>
		/// Clears the error snapshot for the given line in the given text buffer
		/// </summary>
		/// <param name="buffer">The text buffer to which this line belongs</param>
		/// <param name="lineNumber">The line number in the buffer to clear</param>
		public void ClearErrorSnapshot(ITextBuffer buffer, int lineNumber)
		{
			// If this buffer doesn't exist, do nothing
			List<SimileErrorSnapshot> errorList = null;
			if (!liveBufferErrors.TryGetValue(buffer, out errorList) || 
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
			List<SimileErrorSnapshot> errorList = null;
			if (!liveBufferErrors.TryGetValue(buffer, out errorList))
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


		public void VerifyBufferLength(ITextBuffer buffer)
		{
			// If this buffer doesn't exist, do nothing
			List<SimileErrorSnapshot> errorList = null;
			if (!liveBufferErrors.TryGetValue(buffer, out errorList))
				return;

			// If the actual line count is less than what we've been tracking,
			// then remove all excess line elements from the list
			int lineCount = buffer.CurrentSnapshot.LineCount;
			if (lineCount < errorList.Count)
				errorList.RemoveRange(lineCount, errorList.Count - lineCount);
		}

		public void PushAllErrorsToSinks()
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

		public void RegisterLiveBuffer(ITextBuffer buffer)
		{
			if (!liveBufferErrors.ContainsKey(buffer))
				liveBufferErrors.Add(buffer, new List<SimileErrorSnapshot>());
		}

		public void RemoveLiveBuffer(ITextBuffer buffer)
		{
			liveBufferErrors.Remove(buffer);
		}


		#region Error Sink Management

		public void AddSinkManager(SimileErrorSinkManager sink)
		{
			sinkManagers.Add(sink);
		}

		public void RemoveSinkManager(SimileErrorSinkManager sink)
		{
			sinkManagers.Remove(sink);
		}

		#endregion

		#region ITableDataSource

		public string SourceTypeIdentifier => StandardTableDataSources.ErrorTableDataSource;

		public string Identifier => "Console Compare"; // What should this be?

		public string DisplayName => "Console Compare"; // What should this be?

		public IDisposable Subscribe(ITableDataSink sink)
		{
			return new SimileErrorSinkManager(this, sink);
		}

		#endregion
	}
}
