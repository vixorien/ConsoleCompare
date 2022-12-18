using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
		private List<SimileLineOutput> outputLines;
		private List<SimileLineInput> inputLines;

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
			outputLines = new List<SimileLineOutput>();
			inputLines = new List<SimileLineInput>();
		}

		/// <summary>
		/// Adds a simple text line to the list of output
		/// </summary>
		/// <param name="text">Output text</param>
		/// <param name="lineEnding">Type of line ending for this output</param>
		public void AddOutput(string text, LineEndingType lineEnding = LineEndingType.NewLine)
		{
			SimileLineOutput output = new SimileLineOutput(text, lineEnding);

			// Add to the overall lines and the output list
			allLines.Add(output);
			outputLines.Add(output);
		}

		/// <summary>
		/// Adds a simple string for input
		/// </summary>
		/// <param name="text">Input text</param>
		public void AddInput(string text)
		{
			SimileLineInput input = new SimileLineInput(text);

			// Add to the overall lines and the input by itself
			allLines.Add(input);
			inputLines.Add(input);
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
		public string Text { get; private set; }

		public SimileLineInput(string text)
		{
			Text = text;
		}
	}

	/// <summary>
	/// Simple line of output
	/// </summary>
	internal class SimileLineOutput : SimileLine
	{

		// TODO: Output should be made up of one or more output elements
		// - Each element is one of "text", "text from a set", "number", etc.
		// - Final text of the line is a concatenation of all elements in order

		public string Text { get; private set; }

		public LineEndingType LineEnding { get; private set; } // TODO: Rename this

		public SimileLineOutput(string text, LineEndingType lineEnding)
		{
			Text = text;
			LineEnding = lineEnding;
		}

	}
}
