using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell.TableManager;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using System;
using System.Collections.Generic;
using System.IO;

// Details on overall classifier setup: https://stackoverflow.com/a/37602798
// Options for adding errors to error list: https://stackoverflow.com/a/59608426

namespace ConsoleCompare
{
	/// <summary>
	/// Classifier that classifies all text as an instance of the "SimileClassifier" classification type.
	/// </summary>
	internal class SimileClassifier : IClassifier
	{
		/// <summary>
		/// Classification type.
		/// </summary>
		private readonly IClassificationType simileErrorType;
		private readonly IClassificationType simileCommentType;
		private readonly IClassificationType simileInputTagType;
		private readonly IClassificationType simileNumericTagType;

		private ITextBuffer textBuffer;


		/// <summary>
		/// Initializes a new instance of the <see cref="SimileClassifier"/> class.
		/// </summary>
		/// <param name="registry">Classification registry.</param>
		internal SimileClassifier(IClassificationTypeRegistryService registry, ITextBuffer buffer)
		{
			simileErrorType = registry.GetClassificationType(SimileClassifications.SimileErrorClassifier);
			simileCommentType = registry.GetClassificationType(SimileClassifications.SimileCommentClassifier);
			simileInputTagType = registry.GetClassificationType(SimileClassifications.SimileInputTagClassifier);
			simileNumericTagType = registry.GetClassificationType(SimileClassifications.SimileNumericTagClassifier);

			textBuffer = buffer;
			textBuffer.ChangedLowPriority += TextBuffer_ChangedLowPriority;
		}

		/// <summary>
		/// Occurs whenever the text buffer changes (low priority)
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void TextBuffer_ChangedLowPriority(object sender, TextContentChangedEventArgs e)
		{
			// TODO: Handle error parsing here!
			// This gives us a single location to remove/add errors after a full parse
			// This is necessary because classifications happen on a per-line basis,
			//  which means we don't know when to clear all the errors / update them

			// Note: May need to hook this up to something that notifies us when
			// the view is closed so we can remove this event listener
			// See: https://github.com/microsoft/VSSDK-Extensibility-Samples/blob/master/ErrorList/C%23/SpellChecker.cs
		}

		#region IClassifier

#pragma warning disable 67

		/// <summary>
		/// An event that occurs when the classification of a span of text has changed.
		/// </summary>
		/// <remarks>
		/// This event gets raised if a non-text change would affect the classification in some way,
		/// for example typing /* would cause the classification to change in C# without directly
		/// affecting the span.
		/// </remarks>
		public event EventHandler<ClassificationChangedEventArgs> ClassificationChanged;

#pragma warning restore 67

		/// <summary>
		/// Gets all the <see cref="ClassificationSpan"/> objects that intersect with the given range of text.
		/// </summary>
		/// <remarks>
		/// This method scans the given SnapshotSpan for potential matches for this classification.
		/// </remarks>
		/// <param name="span">The span currently being classified.</param>
		/// <returns>A list of ClassificationSpans that represent spans identified to be of this classification.</returns>
		public IList<ClassificationSpan> GetClassificationSpans(SnapshotSpan span)
		{
			// Clear the error list and create a single error snapshot
			SimileErrorSource.Instance.ClearErrors();
			SimileErrorSnapshot errorSnapshot = new SimileErrorSnapshot();

			// Grab possible error details
			int lineNumber = span.Snapshot.GetLineNumberFromPosition(span.Start);
			string filename = GetDocumentFilename(span.Snapshot);

			// Build the results up as we go
			List<ClassificationSpan> results = new List<ClassificationSpan>();

			// Grab the overall text of this span
			string text = span.GetText();

			// Is it a comment?
			if (text.StartsWith(SimileParser.PrefaceComment))
			{
				// Whole line is a comment; add as a comment and return
				results.Add(CreateTagSpan(span, 0, span.Length, simileCommentType));
				return results;
			}

			// Loop through, character by character, looking for start tags
			bool isError = false;
			int numericTagCount = 0;
			int numericTagsOpen = 0;
			int numericTagStart = -1;
			int inputTagCount = 0;
			int inputTagsOpen = 0;
			int inputTagStart = -1;
			for (int i = 0; i < text.Length; i++)
			{
				// Does a tag start or end here?
				if (ContainsAtIndex(text, SimileParser.NumericTagStart, i))
				{
					// New numeric tag starting
					numericTagCount++;
					numericTagsOpen++;
					numericTagStart = i;
				}
				else if (ContainsAtIndex(text, SimileParser.NumericTagEnd, i))
				{
					// Numeric tag ending
					numericTagsOpen--;

					// If we're at zero, we've just finished a tag
					if (numericTagsOpen == 0)
					{
						// Check the tag's validity
						int numericTagEnd = i - numericTagStart + SimileParser.NumericTagEnd.Length;
						string tag = text.Substring(numericTagStart, numericTagEnd);
						bool validTag = SimileParser.ParseNumericTag(tag, null);

						// Create the span either way, but color code based on validity
						results.Add(CreateTagSpan(
							span,
							numericTagStart,
							numericTagEnd,
							validTag ? simileNumericTagType : simileErrorType));

						if (!validTag)
						{
							errorSnapshot.AddError(
								new SimileError()
								{
									DocumentName = filename,
									Text = "Error with numeric tag",
									LineNumber = lineNumber
								});
						}
					}
				}
				else if (ContainsAtIndex(text, SimileParser.InputTagStart, i))
				{
					// New input tag starting
					inputTagCount++;
					inputTagsOpen++;
					inputTagStart = i;

				}
				else if (ContainsAtIndex(text, SimileParser.InputTagEnd, i))
				{
					// Input tag ending
					inputTagsOpen--;

					// If we're at zero here, we've just finished a tag
					if (inputTagsOpen == 0)
					{
						results.Add(CreateTagSpan(
							span,
							inputTagStart,
							i - inputTagStart + SimileParser.InputTagEnd.Length,
							simileInputTagType));
					}
				}

				// Is anything invalid up to this point?
				if (numericTagsOpen < 0 || // Ended before began
					numericTagsOpen > 1 || // Double open symbols
					inputTagsOpen < 0 || // Ended before began
					inputTagsOpen > 1 || // Double open symbols
					(inputTagCount == 1 && numericTagCount > 0)) // Input AND numeric tags together
				{
					isError = true;
					break;
				}
			}

			// Was there an error anywhere on the line?
			if (isError)
			{
				SimileError error = new SimileError()
				{
					Text = "NEED ERROR DETAILS",
					DocumentName = filename,
					LineNumber = lineNumber
				};
				errorSnapshot.AddError(error);

				// Classify the whole line as an error
				results.Add(CreateTagSpan(span, 0, span.Length, simileErrorType));
			}

			// Return our list of classifications (which may be empty)
			SimileErrorSource.Instance.AddError(errorSnapshot);
			return results;
		}

		/// <summary>
		/// Helper for creating a classification span
		/// </summary>
		/// <param name="span">The span upon which this is based</param>
		/// <param name="localStart">The local start position, which will be added to the span's start</param>
		/// <param name="length">The length of this new span</param>
		/// <param name="type">The classification type of this span</param>
		/// <returns>A new classification span</returns>
		private ClassificationSpan CreateTagSpan(SnapshotSpan span, int localStart, int length, IClassificationType type)
		{
			return new ClassificationSpan(new SnapshotSpan(span.Snapshot, new Span(localStart + span.Start, length)), type);
		}

		/// <summary>
		/// Determiens if the given string contains the given value starting at the specified index
		/// </summary>
		/// <param name="str">The string to search</param>
		/// <param name="value">The string to look for</param>
		/// <param name="index">The index to check</param>
		/// <returns>True if the string contains the entire value starting at index, false otherwise</returns>
		private bool ContainsAtIndex(string str, string value, int index)
		{
			int valOffset = 0;
			while (
				valOffset < value.Length &&
				valOffset + index < str.Length &&
				value[valOffset] == str[index])
			{
				valOffset++;
				index++;
			}

			// Did we make it through the search value?
			return valOffset == value.Length;
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

		#endregion
	}
}
