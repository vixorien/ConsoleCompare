using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using System;
using System.Collections.Generic;

// Details on overall classifier setup: https://stackoverflow.com/a/37602798

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
		private readonly IClassificationType simileInputTagType;
		private readonly IClassificationType simileNumericTagType;

		/// <summary>
		/// Initializes a new instance of the <see cref="SimileClassifier"/> class.
		/// </summary>
		/// <param name="registry">Classification registry.</param>
		internal SimileClassifier(IClassificationTypeRegistryService registry)
		{
			simileErrorType = registry.GetClassificationType(SimileClassifications.SimileErrorClassifier);
			simileInputTagType = registry.GetClassificationType(SimileClassifications.SimileInputTagClassifier);
			simileNumericTagType = registry.GetClassificationType(SimileClassifications.SimileNumericTagClassifier);
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
		/// In this instance, it classifies everything and returns each span as a new ClassificationSpan.
		/// </remarks>
		/// <param name="span">The span currently being classified.</param>
		/// <returns>A list of ClassificationSpans that represent spans identified to be of this classification.</returns>
		public IList<ClassificationSpan> GetClassificationSpans(SnapshotSpan span)
		{
			// Build the results up
			List<ClassificationSpan> results = new List<ClassificationSpan>();

			// Grab the overall text of this span
			string text = span.GetText();

			// Find all tag groups
			int currentPosition = 0;
			int tagStart = -1;
			while ((tagStart = text.IndexOf(SimileParser.ElementTagStart, currentPosition)) >= 0)
			{
				// Found a start, move ahead and look for an end
				currentPosition = tagStart + SimileParser.ElementTagStart.Length;
				int tagEnd = text.IndexOf(SimileParser.ElementTagEnd, currentPosition);
				if (tagEnd > tagStart)
				{
					// Found the next end, so we have a complete tag
					int tagStartInSpan = span.Start + tagStart;
					int tagLength = tagEnd - tagStart + SimileParser.ElementTagEnd.Length;
					SnapshotSpan tagSpan = new SnapshotSpan(span.Snapshot, new Span(tagStartInSpan, tagLength));

					// Create an add the classification
					results.Add(new ClassificationSpan(tagSpan, simileNumericTagType));

					// Remember this position
					currentPosition = tagEnd + SimileParser.ElementTagEnd.Length;
				}

			}

			// Return the list, which may be empty
			return results;
		}

		#endregion
	}
}
