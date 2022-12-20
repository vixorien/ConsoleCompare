using System;
using System.IO;
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
	/// 
	/// # Blank lines are ignored, but lines with just '.' mean
	/// # to expect a blank in the output
	/// 
	/// # Line notation:
	/// # # means comment
	/// # . means a standard line that ends with a newline, effectively Console.WriteLine()
	/// # ; means a line without a newline, generally right before user input, effectively Console.Write()
	/// # > means input from the user
	/// #
	/// # Note: Any other starting character is considered invalid
	/// 
	/// .Hello, World!
	/// ;What is your name?
	/// >Chris
	/// .Hello, Chris!
	/// .
	/// ;What is your age?
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
		/// Parses a file into a ConsoleSimile object if possible
		/// </summary>
		/// <param name="filePath">Path to the file</param>
		/// <returns>A new ConsoleSimile object containing the simile</returns>
		public static ConsoleSimile ParseFromFile(string filePath)
		{
			string[] lines = File.ReadAllLines(filePath);
			return Parse(lines);
		}

		/// <summary>
		/// Parses an array of lines into a ConsoleSimile object
		/// </summary>
		/// <param name="lines">Array of lines to parse</param>
		/// <returns>A new ConsoleSimile object containing the simile</returns>
		public static ConsoleSimile Parse(string[] lines)
		{
			if (lines == null || lines.Length == 0)
				return null;

			// Loop through lines and process each
			ConsoleSimile result = new ConsoleSimile();
			foreach (string line in lines)
			{
				bool success = ParseLine(line, result);
				
				// TODO: Should an invalid parse negate the whole simile?
				//       Or just be ignored?
			}

			return result;
		}

		/// <summary>
		/// Parses a single line and adds the result, if any, to the simile
		/// </summary>
		/// <param name="line">Line to parse</param>
		/// <param name="simile">Simile to add to</param>
		/// <returns>True if the line is valid or blank, false if invalid</returns>
		private static bool ParseLine(string line, ConsoleSimile simile)
		{
			// Check for empty line - return true as empty lines are perfectly valid
			if (string.IsNullOrEmpty(line))
				return true;

			// Check the first character
			switch (line[0])
			{
				// Simple parsing for now
				case '.': simile.AddOutput(line.Substring(1), LineEndingType.NewLine); break;
				case ';': simile.AddOutput(line.Substring(1), LineEndingType.SameLine); break;
				case '>': simile.AddInput(line.Substring(1)); break;

				// Comments: Simply ignore, but here if we want to do something
				case '#': break;

				// Any other character results in an invalid parse
				default: return false;
			}

			// Valid line, even if a comment
			return true;
		}
	}
}
