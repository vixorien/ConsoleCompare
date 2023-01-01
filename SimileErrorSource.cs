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
