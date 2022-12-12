using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

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

		// Iterator functionality (maybe turn into IEnumerable<string>?)
		private int currentOutputIndex = 0;

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
		public void AddOutput(string text)
		{
			SimileLineOutput output = new SimileLineOutput(text);

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
		/// Immediately sends all input to the given stream writer
		/// </summary>
		/// <param name="writer">Writer to which we write each line</param>
		public void SendAllInput(StreamWriter writer)
		{
			foreach (SimileLineInput input in inputLines)
				writer.WriteLine(input.Text);
		}

		/// <summary>
		/// Gets the next line of expected output
		/// </summary>
		/// <returns>The next line of output, or null if there is no more</returns>
		public string GetNextOutput()
		{
			if (currentOutputIndex >= outputLines.Count)
				return null;

			return outputLines[currentOutputIndex++].Text;
		}

		/// <summary>
		/// Resets the output iteration back to the first line
		/// </summary>
		public void ResetOutputIteration()
		{
			currentOutputIndex = 0;
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

		public SimileLineOutput(string text)
		{
			Text = text;
		}

	}
}
