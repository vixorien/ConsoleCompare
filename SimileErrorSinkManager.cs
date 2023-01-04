using Microsoft.VisualStudio.Shell.TableManager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleCompare
{
	/// <summary>
	/// Manages one of the sinks which collects entries/snapshots to
	/// be placed into the error list table in visual studio
	/// </summary>
	internal class SimileErrorSinkManager : IDisposable
	{
		private ITableDataSink sink;
		private SimileErrorSource errorSource;

		/// <summary>
		/// 
		/// </summary>
		/// <param name="errorSource"></param>
		/// <param name="sink"></param>
		public SimileErrorSinkManager(SimileErrorSource errorSource, ITableDataSink sink)
		{
			this.errorSource = errorSource;
			this.sink = sink;
			errorSource.AddSinkManager(this);
		}

		/// <summary>
		/// Add one or more snapshots to the sink
		/// </summary>
		/// <param name="snapshots">List of snapshots to add</param>
		public void AddErrorSnapshots(List<SimileErrorSnapshot> snapshots)
		{
			foreach (SimileErrorSnapshot snap in snapshots)
				if(snap != null)
					sink.AddSnapshot(snap);
		}

		/// <summary>
		/// Clear all snapshots from the sink
		/// </summary>
		public void ClearAllSnapshots()
		{
			sink.RemoveAllSnapshots();
		}

		/// <summary>
		/// Cleans up this object, removing all snapshots from the sink and 
		/// removing this from the error source
		/// </summary>
		public void Dispose()
		{
			// Clean up first
			sink.RemoveAllSnapshots();
			errorSource.RemoveSinkManager(this);
		}
	}
}
