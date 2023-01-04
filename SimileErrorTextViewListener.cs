using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using System.Collections.Generic;
using System.ComponentModel.Composition;

namespace ConsoleCompare
{
	/// <summary>
	/// Allows us to listen for text views (essentially document tabs in visual studio)
	/// of simile files being opened and closed (connected and disconnected)
	/// </summary>
	[Export(typeof(ITextViewConnectionListener))]
	[ContentType("simile")] // Only simile files
	[TextViewRole(PredefinedTextViewRoles.Interactive)]
	internal class SimileErrorTextViewListener : ITextViewConnectionListener
	{
		// Track buffers along with their current snapshots
		private Dictionary<ITextBuffer, List<SimileError>> liveBuffers = new Dictionary<ITextBuffer, List<SimileError>>();
		
		/// <summary>
		/// Callback for when a text view of a simile file is opened
		/// </summary>
		/// <param name="textView">The text view itself</param>
		/// <param name="reason">The reason for this connection</param>
		/// <param name="subjectBuffers">The text buffers within the view</param>
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

		/// <summary>
		/// Callback for when a text view of a simile file is closed
		/// </summary>
		/// <param name="textView">The text view itself</param>
		/// <param name="reason">The reason for this disconnect</param>
		/// <param name="subjectBuffers">The text buffers within the view</param>
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

		/// <summary>
		/// Callback for a text buffer having its content changed.  This is when we
		/// push any errors that have been classified down to the error sinks which
		/// will display them in VS's error list.
		/// </summary>
		private void Buffer_ChangedLowPriority(object sender, TextContentChangedEventArgs e)
		{
			// Report the new length, in the event lines with errors were removed!
			SimileErrorSource.Instance.VerifyBufferLength(sender as ITextBuffer);

			// Push all errors to sinks since something has changed
			SimileErrorSource.Instance.RefreshAllErrorsInSinks();
		}


	
	}
}
