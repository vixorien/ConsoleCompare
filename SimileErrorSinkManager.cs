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
		private List<SimileErrorSnapshot> sinkSnapshots;

		public SimileErrorSinkManager(SimileErrorSource errorSource, ITableDataSink sink)
		{
			this.errorSource = errorSource;
			this.sink = sink;
			sinkSnapshots = new List<SimileErrorSnapshot>();
			errorSource.AddSinkManager(this);
		}

		public void Clear()
		{
			sink.RemoveAllSnapshots();
			sinkSnapshots.Clear();
		}

		public void UpdateSink(IEnumerable<SimileErrorSnapshot> newSnapshots)
		{
			// Should we clear first?


			foreach (SimileErrorSnapshot newSnap in newSnapshots)
			{
				// Do we know about this snapshot yet?
				if (!sinkSnapshots.Contains(newSnap))
				{
					// Add to our list AND the sink itself
					sinkSnapshots.Add(newSnap);
					sink.AddSnapshot(newSnap);
				}
				//int index = sinkSnapshots.IndexOf(newSnap);
				//if (index == -1)
				//{
				//	// Not found, so add
				//	sink.AddSnapshot(newSnap);
				//}
				//else
				//{
				//	// Snapshot already exists so replace
				//	// NOTE: Probably unnecessary?  Unless we're
				//	// literally replacing with a similar-but-different one
				//	sink.ReplaceSnapshot(sinkSnapshots[index], newSnap);
				//	sinkSnapshots.Remove(newSnap);
				//}

				//// Either way put this into our list
				//sinkSnapshots.Add(newSnap);
			}
		}

		public void Dispose()
		{
			errorSource.RemoveSinkManager(this);
		}
	}
}
