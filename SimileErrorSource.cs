using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.TableControl;
using Microsoft.VisualStudio.Shell.TableManager;
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
		private List<SimileErrorSinkManager> sinks;
		private List<SimileErrorSnapshot> errorSnapshots;

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

			sinks = new List<SimileErrorSinkManager>();
			errorSnapshots = new List<SimileErrorSnapshot>();

			// Ref: https://github.com/madskristensen/WebAccessibilityChecker/tree/master/src/ErrorList
			// Before we can add a source, this class needs to implement ITableDataSource - This class can hold all of the "error snapshots"
			// Then we need a sinkmanager?  Which will hold (but not implement) an ITableDataSink
			// Then we need something that implements WpfTableEntriesSnapshotBase to act as our "error snapshots"
			// And then...?

			// Ensure our imports are complete before moving on
			IComponentModel compositionService = ServiceProvider.GlobalProvider.GetService(typeof(SComponentModel)) as IComponentModel;
			compositionService?.DefaultCompositionService.SatisfyImportsOnce(this);

			ITableManager table = TableManagerProvider.GetTableManager(StandardTables.ErrorsTable);
			table.AddSource(
				this,
				StandardTableColumnDefinitions.Line,
				StandardTableColumnDefinitions.Text,
				StandardTableColumnDefinitions.DocumentName);
		}

		public void AddError(SimileErrorSnapshot error)
		{
			// Add an error to our list and update sinks
			errorSnapshots.Add(error);
			UpdateSinkManagers();
		}

		public void ClearErrors()
		{
			foreach (SimileErrorSinkManager sink in sinks)
				sink.Clear();
		}

		#region Error Sink Management

		public void AddSinkManager(SimileErrorSinkManager sink)
		{
			sinks.Add(sink);
		}

		public void RemoveSinkManager(SimileErrorSinkManager sink)
		{
			sinks.Remove(sink);
		}

		public void UpdateSinkManagers()
		{
			foreach (SimileErrorSinkManager sink in sinks)
			{
				sink.UpdateSink(errorSnapshots);
			}
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
