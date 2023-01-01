using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
		private Dictionary<ITextBuffer, SimileErrorSnapshot> liveBuffers = new Dictionary<ITextBuffer, SimileErrorSnapshot>();



		public void SubjectBuffersConnected(ITextView textView, ConnectionReason reason, IReadOnlyCollection<ITextBuffer> subjectBuffers)
		{
			// Add any new buffers to the live buffer list and hook up their event
			foreach (ITextBuffer buffer in subjectBuffers)
			{
				// If we're not tracking this buffer, add to the list and subscribe
				if (!liveBuffers.ContainsKey(buffer))
				{
					SimileErrorSnapshot snapshot = new SimileErrorSnapshot();
					liveBuffers.Add(buffer, snapshot);
					buffer.ChangedLowPriority += Buffer_ChangedLowPriority;

					// Add this snapshot to the error source
					SimileErrorSource.Instance.AddErrorSnapshot(snapshot);

					// Also perform an initial parse for errors
					ParseForErrors(buffer);
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
					// Yes.  Remove from error source and our own dictionary
					SimileErrorSource.Instance.RemoveErrorSnapshot(liveBuffers[buffer]); 
					liveBuffers.Remove(buffer);
					
					// Also unsubscribe from changes
					buffer.ChangedLowPriority -= Buffer_ChangedLowPriority;
				}
			}
		}


		private void Buffer_ChangedLowPriority(object sender, TextContentChangedEventArgs e)
		{
			// Parse the new text and report errors!
			ITextBuffer buffer = sender as ITextBuffer;
			ParseForErrors(buffer);
		}

		private void ParseForErrors(ITextBuffer buffer)
		{
			// Get the file name
			string filename = GetDocumentFilename(buffer.CurrentSnapshot);

			// Create the snapshot
			SimileError error = new SimileError()
			{
				Text = "Called Parse For Errors",
				DocumentName = filename,
				LineNumber = 99
			};
			liveBuffers[buffer].AddError(error);

		}

		private string GetDocumentFilename(ITextSnapshot snapshot)
		{
			// Grab the full path and strip down to filename
			string path = GetDocumentPath(snapshot);
			if (!string.IsNullOrEmpty(path))
				return Path.GetFileName(path);

			// Path/filename not found
			return null;
		}


		private string GetDocumentPath(ITextSnapshot snapshot)
		{
			// Attempt to get the text document from the snapshot and return the filepath
			ITextDocument doc = null;
			if (snapshot.TextBuffer.Properties.TryGetProperty(typeof(ITextDocument), out doc) && doc != null)
				return doc.FilePath;

			// Unable to find text document
			return null;
		}
	}
}
