using Microsoft.VisualStudio.Shell.TableManager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleCompare
{
	internal class SimileErrorSinkManager : IDisposable
	{
		private ITableDataSink sink;
		private SimileErrorSource errorSource;

		public SimileErrorSinkManager(SimileErrorSource errorSource, ITableDataSink sink)
		{
			this.errorSource = errorSource;
			this.sink = sink;
			errorSource.AddSinkManager(this);
		}

		public void AddErrorSnapshots(List<SimileErrorSnapshot> snapshots)
		{
			foreach (SimileErrorSnapshot snap in snapshots)
				if(snap != null)
					sink.AddSnapshot(snap);
		}

		public void ClearAllSnapshots()
		{
			sink.RemoveAllSnapshots();
		}

		

		public void UpdateSink(IEnumerable<SimileErrorSnapshot> newSnapshots)
		{
			// Should we clear first?
			//Clear();

			//foreach (SimileErrorSnapshot newSnap in newSnapshots)
			//{
			//	// Do we know about this snapshot yet?
			//	if (!sinkSnapshots.Contains(newSnap))
			//	{
			//		// Add to our list AND the sink itself
			//		sinkSnapshots.Add(newSnap);
			//		sink.AddSnapshot(newSnap);
			//	}
			//}
		}

		public void Dispose()
		{
			// Clean up first
			sink.RemoveAllSnapshots();
			errorSource.RemoveSinkManager(this);
		}
	}
}
