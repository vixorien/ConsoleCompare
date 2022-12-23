using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Windows.Media.Animation;
using System.Windows.Navigation;

namespace ConsoleCompare
{
	/// <summary>
	/// Represents a set of console IO for comparison
	/// </summary>
	internal class ConsoleSimile
	{
		// Overall requirements:
		// - Multiples lines (probably just a List of them in order)
		// - Each line could be input or output (abstract classes for each?)
		// - A line can have multiple "pieces", or elements, like:
		//   - A span of regular text
		//   - A "variable" that might change based on prior input/output
		//   - A number that needs to be parsed (for validity?)
		//   - A number within a specified range
		//   - An element from a specific set
		// - Input lines...
		//   - A single string
		//   - A number in a range?
		//   - A "variable" that might change?
		//   - An element from a specific set?
		// - Maybe be able to "step through" like an iterator?

		private List<SimileLine> allLines;

		/// <summary>
		/// Gets the count of all lines (input and output) in the simile
		/// </summary>
		public int Count => allLines.Count;

		/// <summary>
		/// Gets a line, either input or output, from the simile
		/// </summary>
		/// <param name="index">Index of the line to retrieve</param>
		/// <returns>A simile line, either input or output</returns>
		public SimileLine this[int index] => allLines[index];

		/// <summary>
		/// Creates a new, empty console simile
		/// </summary>
		public ConsoleSimile()
		{
			allLines = new List<SimileLine>();
		}

		/// <summary>
		/// Adds a simple text line to the list of output
		/// </summary>
		/// <param name="text">Output text</param>
		/// <param name="lineEnding">Type of line ending for this output</param>
		public void AddOutput(string text, LineEndingType lineEnding = LineEndingType.NewLine)
		{
			SimileLineOutput output = new SimileLineOutput(text, lineEnding);
			allLines.Add(output);
		}

		
		public void AddOutput(SimileLineOutput output)
		{
			allLines.Add(output);
		}

		/// <summary>
		/// Adds a simple string for input
		/// </summary>
		/// <param name="text">Input text</param>
		public void AddInput(string text)
		{
			SimileLineInput input = new SimileLineInput(text);
			allLines.Add(input);
		}


		/// <summary>
		/// Static helper for loading an entire simile from a file
		/// </summary>
		/// <param name="filePath">The file path for the simile file</param>
		/// <returns>A new simile loaded from the file</returns>
		public static ConsoleSimile LoadFromFile(string filePath)
		{
			// Read the lines from the file
			string[] lines = null;
			try
			{
				lines = File.ReadAllLines(filePath);
			}
			catch (Exception e)
			{
				throw new InvalidOperationException("Unable to load simile from file", e);
			}

			// Parse whatever we read
			return SimileParser.Parse(lines);
		}
	}


	/// <summary>
	/// Possible line endings for simile output
	/// </summary>
	public enum LineEndingType
	{
		NewLine,
		SameLine
	}

	/// <summary>
	/// Base class for lines of IO
	/// </summary>
	internal abstract class SimileLine
	{
	}

	/// <summary>
	/// Simple line of input
	/// </summary>
	internal class SimileLineInput : SimileLine
	{
		public string Text { get; }

		public SimileLineInput(string text) => Text = text;
	}

	/// <summary>
	/// A single line of output
	/// </summary>
	internal class SimileLineOutput : SimileLine
	{
		// Overall output is made up of multiple output elements	
		private List<SimileOutputElement> outputElements;

		/// <summary>
		/// Gets the type of line ending for this output
		/// </summary>
		public LineEndingType LineEnding { get; }

		/// <summary>
		/// Gets the raw text of this line, containing any
		/// numeric output tags
		/// </summary>
		public string RawText { get; }


		public SimileLineOutput(string rawText, LineEndingType lineEnding)
		{
			LineEnding = lineEnding;
			RawText = rawText;
			outputElements = new List<SimileOutputElement>();
		}

		/// <summary>
		/// Adds a simple text element to the line
		/// </summary>
		/// <param name="text">Simple text data</param>
		public void AddTextElement(string text)
		{
			outputElements.Add(new SimileOutputText(text));
		}

		/// <summary>
		/// Adds a numeric element to the line
		/// </summary>
		/// <typeparam name="T">The type of data</typeparam>
		/// <param name="type">The type of numeric data</param>
		/// <param name="min">The inclusive minimum expected value, or null for no minimum</param>
		/// <param name="max">The inclusive maximum expected value, or null for no maximum</param>
		/// <param name="set">The set of valid values, or null for no expected values</param>
		/// <param name="precision">The amount of precision for rounding, or null for no rounding</param>
		public void AddNumericElement<T>(SimileNumericType type, T? min = null, T? max = null, List<T> set = null, int? precision = null)
			where T : struct, IComparable
		{
			SimileOutputNumeric<T> number = new SimileOutputNumeric<T>(type)
			{
				Minimum = min,
				Maximum = max,
				Precision = precision
			};

			if(set != null)
				number.ValueSet.AddRange(set);

			outputElements.Add(number);
		}


		/// <summary>
		/// Adds a complete numeric element to the line
		/// </summary>
		/// <typeparam name="T">The type of data</typeparam>
		/// <param name="numericElement">The complete numeric element to add</param>
		public void AddNumericElement<T>(SimileOutputNumeric<T> numericElement)
			where T:struct, IComparable
		{
			outputElements.Add(numericElement);
		}

		/// <summary>
		/// Compares the overall line (made up of all output elements in order)
		/// to the given string
		/// </summary>
		/// <param name="comparison">String for comparison</param>
		/// <returns>True if the lines are equivalent, false otherwise</returns>
		public bool CompareLine(string comparison)
		{
			// Null is always incorrect
			if (comparison == null)
				return false;

			// Loop through the elements in order and verify matches
			foreach (SimileOutputElement element in outputElements)
			{
				if (!element.AtBeginningOf(comparison, out comparison))
					return false;
			}

			// All elements matched
			return true;
		}

	}

	/// <summary>
	/// Represents a single element of a larger output line
	/// </summary>
	internal abstract class SimileOutputElement {
		/// <summary>
		/// Determines if this element is at the beginning of the
		/// given string and sends out the remainder of the string
		/// </summary>
		/// <param name="line">The line to check</param>
		/// <param name="remainder">What's left after this element is removed</param>
		/// <returns>True if this elements starts the line, false otherwise</returns>
		public abstract bool AtBeginningOf(string line, out string remainder);
	}

	/// <summary>
	/// Represents a single text element of a larger output line
	/// </summary>
	internal class SimileOutputText : SimileOutputElement
	{
		public string Text { get; }
		public SimileOutputText(string text) => Text = text;

		/// <summary>
		/// Determines if this element is at the beginning of the
		/// given string and sends out the remainder of the string
		/// </summary>
		/// <param name="line">The line to check</param>
		/// <param name="remainder">What's left after this element is removed</param>
		/// <returns>True if this elements starts the line, false otherwise</returns>
		public override bool AtBeginningOf(string line, out string remainder)
		{
			bool starts = line.StartsWith(Text);
			if (starts)
			{
				remainder = line.Substring(Text.Length);
				return true;
			}
			else
			{
				remainder = line;
				return false;
			}
		}
	}

	/// <summary>
	/// Represents a single numeric element of a larger output line
	/// </summary>
	/// <typeparam name="T">The numeric data type of the element</typeparam>
	internal class SimileOutputNumeric<T> : SimileOutputElement
		where T : struct, IComparable
	{
		public SimileNumericType NumericType { get; }
		public T? Minimum { get; set; }
		public T? Maximum { get; set; }
		public List<T> ValueSet { get; }
		public int? Precision { get; set; }

		public SimileOutputNumeric(SimileNumericType type)
		{
			NumericType = type;
			ValueSet = new List<T>();
			
		}

		/// <summary>
		/// Determines if this element is at the beginning of the
		/// given string and sends out the remainder of the string
		/// </summary>
		/// <param name="line">The line to check</param>
		/// <param name="remainder">What's left after this element is removed</param>
		/// <returns>True if this elements starts the line, false otherwise</returns>
		public override bool AtBeginningOf(string line, out string remainder)
		{
			// Go up to the next space or the end (or a single character for characters)
			int length = line.Length;
			int space = line.IndexOf(' ');
			if (NumericType == SimileNumericType.Char)
			{
				length = 1;
			}
			else if(space != -1)
			{
				length = space;
			}

			// Chop up the string and attempt a parse
			string valString = line.Substring(0, length);
			remainder = line.Substring(length);
			T val = default;

			try
			{
				val = (T)Convert.ChangeType(valString, typeof(T));
			}
			catch 
			{
				remainder = line;
				return false; 
			}

			// Successful parse, so verify other options
			if (Minimum.HasValue && val.CompareTo(Minimum.Value) < 0) return false;
			if (Maximum.HasValue && val.CompareTo(Maximum.Value) > 0) return false;
			if (ValueSet != null && ValueSet.Count > 0 && !ValueSet.Contains(val)) return false;
			// TODO: Handle precision

			// Adjust the remainder and success
			remainder = line.Substring(length);
			return true;
		}
	}

	/// <summary>
	/// Represents allowable numeric types for numeric simile elements
	/// </summary>
	internal enum SimileNumericType
	{
		Byte,
		SignedByte,
		Char,
		Short,
		UnsignedShort,
		Int,
		UnsignedInt,
		Long,
		UnsignedLong,
		Float,
		Double,
		Unknown
	}
}
