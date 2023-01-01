using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace ConsoleCompare
{
	[Export(typeof(ITextViewConnectionListener))]
	[ContentType("simile")] // Only simile files
	[TextViewRole(PredefinedTextViewRoles.Interactive)]
	internal class SimileErrorTextViewListener : ITextViewConnectionListener
	{
		// Track buffers along with their current snapshots
		private Dictionary<ITextBuffer, List<SimileError>> liveBuffers = new Dictionary<ITextBuffer, List<SimileError>>();
		

		public void SubjectBuffersConnected(ITextView textView, ConnectionReason reason, IReadOnlyCollection<ITextBuffer> subjectBuffers)
		{
			// Add any new buffers to the live buffer list and hook up their event
			foreach (ITextBuffer buffer in subjectBuffers)
			{
				// If we're not tracking this buffer, add to the list and subscribe
				if (!liveBuffers.ContainsKey(buffer))
				{
					// Track this buffer and subscribe to its change event
					liveBuffers.Add(buffer, new List<SimileError>());
					buffer.ChangedLowPriority += Buffer_ChangedLowPriority;

					// Register this buffer with the error source
					SimileErrorSource.Instance.RegisterLiveBuffer(buffer);
				}
			}
		}


		public void SubjectBuffersDisconnected(ITextView textView, ConnectionReason reason, IReadOnlyCollection<ITextBuffer> subjectBuffers)
		{
			// Determine if the disconnecting buffers are in our list and, if so, remove
			foreach (ITextBuffer buffer in subjectBuffers)
			{
				// Were we tracking this buffer?
				if (liveBuffers.ContainsKey(buffer))
				{
					// Remove this buffer from the error source and unsubscribe from its changes
					SimileErrorSource.Instance.RemoveLiveBuffer(buffer);
					buffer.ChangedLowPriority -= Buffer_ChangedLowPriority;
				}
			}
		}


		private void Buffer_ChangedLowPriority(object sender, TextContentChangedEventArgs e)
		{
			// Report the new length, in the event lines with errors were removed!
			SimileErrorSource.Instance.VerifyBufferLength(sender as ITextBuffer);

			// Push all errors to sinks since something has changed
			SimileErrorSource.Instance.PushAllErrorsToSinks();
		}


	
	}
}
