using System;
using System.Collections.Generic;

namespace ConsoleCompare
{
	/// <summary>
	/// Helper class for parsing a ConsoleSimile from an array of lines
	/// </summary>
	/// <example>
	/// An example simile file might look like this:
	/// 
	/// # Comment
	/// # Below . means output and > means input
	/// 
	/// # Blank lines are ignored, but lines with just '.' mean
	/// # to expect a blank in the output
	/// 
	/// .Hello, World!
	/// .What is your name?
	/// >Chris
	/// .Hello, Chris!
	/// .
	/// .What is your age?
	/// >7
	/// .You are 7 years old?  That's over 2555 days!
	///
	/// # Another comment
	/// .Thank you for playing
	/// 
	/// </example>
	internal static class SimileParser
	{
		/// <summary>
		/// Parses an array of lines into a ConsoleSimile object
		/// </summary>
		/// <param name="lines">Array of lines to parse</param>
		public static ConsoleSimile Parse(string[] lines)
		{
			ConsoleSimile result = new ConsoleSimile();
			if (lines == null || lines.Length == 0)
				return result;

			// Loop through lines and process each
			foreach (string line in lines)
			{
				ParseLine(line, result);
			}

			return result;
		}

		/// <summary>
		/// Parses a single line and adds the result, if any, to the simile
		/// </summary>
		/// <param name="line">Line to parse</param>
		/// <param name="simile">Simile to add to</param>
		private static void ParseLine(string line, ConsoleSimile simile)
		{
			// Check for invalid line
			if (string.IsNullOrEmpty(line))
				return;

			// Check the first character
			switch (line[0])
			{
				// Simple parsing for now
				case '.': simile.AddOutput(line.Substring(1)); break;
				case '>': simile.AddInput(line.Substring(1)); break;

				// Comments: Simply ignore, but here if we want to do something
				case '#': break;

				// Other: Ignoring for now, but maybe throw an exception to 
				// display a message about an invalid format?
				default: break;
			}
		}
	}
}
