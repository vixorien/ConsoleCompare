using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.VisualStudio.Shell.Interop;
using System.Linq.Expressions;
using System.CodeDom;

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
	/// # ; means a line without a newline, generally right before user input, effectively Console.Write() - these do NOT support numeric tags
	/// # > means input from the user
	/// #
	/// # Note: Any other starting character is considered invalid
	/// 
	/// # Numeric tags:
	/// # - Numeric data that needs to be parsed and checked for validity can be
	/// #   denoted inside double square brackets: [[ ]]'s 
	/// # - Several options exist for data validation (see below)
	/// # - Multiple options can be combined in a single [[ ]] with semicolons
	/// # - NOTE: non-newline lines do not support numeric tags, as they need to have a finite length
	/// # 
	/// # Syntax:
	/// # - type (required)
	/// #   - supported types: byte, sbyte, short, ushort, int, uint, long, ulong, float, double, char
	/// #   - short names: b, sb, s, us, i, ui, l, ul, f, d, c
	/// #   - examples: [[t=s]] or [[type=s]] or [[t=short]] would expect a short
	/// #
	/// # - min / max (optional)
	/// #   - inclusive minimum and/or inclusive maximum for parsed numeric value
	/// #   - examples: [[min=-10]] or [[min=3.14159]] or [[min=1979]] or [[max=99]] or [[min=-5;max=5]]
	/// #
	/// # - value set (optional)
	/// #   - a set of one or more expected values
	/// #   - examples: [[v={1,2,3,4}]] or [[values={5,10,15,20}]] or [[v={88}]]
	/// #
	/// # - precision (optional)
	/// #   - Acceptable precision bounds (mostly for floating point rounding errors)
	/// #   - Must be an integer between 0-15 (inclusive)
	/// #   - Rounds the results to the given precision for checking
	/// #   - examples: [[precision=3]] or [[p=5]]
	/// 
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
		// Parsing details
		private const char PrefaceOutputWithNewLine = '.';
		private const char PrefaceOutputWithoutNewLine = ';';
		private const char PrefaceInput = '>';
		private const char PrefaceComment = '#';
		private const string ElementTagStart = "[[";
		private const string ElementTagEnd = "]]";
		private const string ElementOptionDelimeter = ";";
		private const string ElementOptionEquals = "=";
		private const string ElementValueSetStart = "{";
		private const string ElementValueSetEnd = "}";


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
			if (lines == null)
				throw new ArgumentNullException("lines", "Array of lines to parse was null");

			if (lines.Length == 0)
				throw new SimileParseException("No lines to parse; was the file empty?");

			// Loop through lines and process each
			ConsoleSimile result = new ConsoleSimile();
			int lineNumber = 1;
			foreach (string line in lines)
			{
				// If the parse fails, throw an exception to pass failure details up
				// - TODO: Capture more details on the failure reason
				if (!ParseLine(line, result))
					throw new SimileParseException("Error parsing line " + lineNumber, line, lineNumber);

				lineNumber++;
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
				case PrefaceOutputWithNewLine:
					{
						// Remove the first character
						line = line.Substring(1);

						// Create the output line and parse
						SimileLineOutput output = new SimileLineOutput(line, LineEndingType.NewLine);
						if (!ParseOutputElements(line, output))
							return false;

						// Success
						simile.AddOutput(output);
						break;
					}

				case PrefaceOutputWithoutNewLine:
					{
						// Remove the first character
						line = line.Substring(1);

						// Create the output line and parse
						SimileLineOutput output = new SimileLineOutput(line, LineEndingType.SameLine);
						if (!ParseOutputElements(line, output))
							return false;

						// Success
						simile.AddOutput(output);
						break;
					}

				// Input is very basic at the moment
				case PrefaceInput: simile.AddInput(line.Substring(1)); break;

				// Comments: Simply ignore, but here if we want to do something
				case PrefaceComment: break;

				// Any other character results in an invalid parse
				default: return false;
			}

			// Valid line, even if a comment
			return true;
		}

		/// <summary>
		/// Recursive helper to parse the elements of a single line of output
		/// </summary>
		/// <param name="line">The string of output to parse</param>
		/// <param name="output">The output line to add elements to</param>
		/// <returns>True if the parse is successful, false otherwise</returns>
		private static bool ParseOutputElements(string line, SimileLineOutput output)
		{
			// Empty lines are valid
			if (line.Length == 0)
				return true;

			// Search for numeric tags
			int startIndex = line.IndexOf(ElementTagStart);
			int endIndex = line.IndexOf(ElementTagEnd);

			// If the end tag is first, we're invalid
			if (endIndex < startIndex)
				return false;

			// If one or the other but not both exist, this is an invalid line
			if (startIndex == -1 ^ endIndex == -1) // XOR
				return false;

			// Check line contents
			if (startIndex == -1)
			{
				// No numeric data, so this is one big text string
				output.AddTextElement(line);
				return true;
			}

			// Only regular lines (ending with a newline character) can have numeric tags
			if (output.LineEnding == LineEndingType.SameLine)
				return false;

			// Is there anything before the numeric element?
			if (startIndex > 0)
			{
				// Add the starting text to the output
				string lineStartText = line.Substring(0, startIndex);
				output.AddTextElement(lineStartText);

				// Strip the start text from the line
				line = line.Substring(startIndex);

				// Adjust indices accordingly
				endIndex -= startIndex;
				startIndex = 0;
			}

			// Grab the tag and the remainder		
			string tag = line.Substring(startIndex + ElementTagStart.Length, endIndex - startIndex - ElementTagStart.Length);
			string remainder = line.Substring(endIndex + ElementTagEnd.Length);

			// Parse the tag
			if (!ParseNumericTag(tag, output))
				return false;

			// Recursively handle the remainder
			return ParseOutputElements(remainder, output);
		}

		/// <summary>
		/// Parses the contents of a numeric tag from an output line.  This assumes the start and end 
		/// characters [[ ]]'s have already been stripped away.
		/// </summary>
		/// <param name="tag">The tag from the string</param>
		/// <param name="output">The output to add the numeric element to</param>
		/// <returns>True if the tag is parsed successfully, false otherwise</returns>
		private static bool ParseNumericTag(string tag, SimileLineOutput output)
		{
			// Split into options
			string[] allOptions = tag.Split(new string[]{ ElementOptionDelimeter}, StringSplitOptions.RemoveEmptyEntries);

			// Process each option
			SimileNumericType type = SimileNumericType.Unknown;
			string min = null;
			string max = null;
			string[] values = null;
			string precision = null;

			foreach (string op in allOptions)
			{
				// Split across the equals and check
				string[] pieces = op.Split(new string[] { ElementOptionEquals }, StringSplitOptions.RemoveEmptyEntries);
				if (pieces.Length != 2)
					return false;

				// Trim both halves before checking
				pieces[0] = pieces[0].Trim();
				pieces[1] = pieces[1].Trim();
				switch (pieces[0])
				{
					case "t":
					case "type":
						type = ParseType(pieces[1]);
						if (type == SimileNumericType.Unknown)
							return false;
						break;

					case "min":
						min = pieces[1];
						break;

					case "max":
						max = pieces[1];
						break;

					case "v":
					case "values":
						// Verify set has { }'s around the values
						if (!pieces[1].StartsWith(ElementValueSetStart) || 
							!pieces[1].EndsWith(ElementValueSetEnd))
							return false;

						// Strip the start and end pieces, then split
						pieces[1] = pieces[1].Substring(ElementValueSetStart.Length, pieces[1].Length - ElementValueSetStart.Length - ElementValueSetEnd.Length);
						values = pieces[1].Split(',');
						break;

					case "p":
					case "precision":
						precision = pieces[1];
						break;

					// Unknown option
					default:
						return false;
				}
			}

			// Check the type and create the corresponding numeric element
			switch (type)
			{
				case SimileNumericType.Byte: return CreateNumericElement<byte>(type, min, max, values, precision, output);
				case SimileNumericType.SignedByte: return CreateNumericElement<sbyte>(type, min, max, values, precision, output);
				case SimileNumericType.Char: return CreateNumericElement<char>(type, min, max, values, precision, output);
				case SimileNumericType.Short: return CreateNumericElement<short>(type, min, max, values, precision, output);
				case SimileNumericType.UnsignedShort: return CreateNumericElement<ushort>(type, min, max, values, precision, output);
				case SimileNumericType.Int: return CreateNumericElement<int>(type, min, max, values, precision, output);
				case SimileNumericType.UnsignedInt: return CreateNumericElement<uint>(type, min, max, values, precision, output);
				case SimileNumericType.Long: return CreateNumericElement<long>(type, min, max, values, precision, output);
				case SimileNumericType.UnsignedLong: return CreateNumericElement<ulong>(type, min, max, values, precision, output);
				case SimileNumericType.Float: return CreateNumericElement<float>(type, min, max, values, precision, output);
				case SimileNumericType.Double: return CreateNumericElement<double>(type, min, max, values, precision, output);
				
				// Invalid type found
				case SimileNumericType.Unknown:
				default:
					return false;
			}
		}

		/// <summary>
		/// Creates a numeric element of the specified type from the given strings
		/// </summary>
		/// <typeparam name="T">The data type of the numeric element</typeparam>
		/// <param name="type">The type</param>
		/// <param name="min">Minimum value, or null for none</param>
		/// <param name="max">Maximum value, or null for none</param>
		/// <param name="values">Array of values, or null for none</param>
		/// <param name="precision">Precision value, or null for none</param>
		/// <param name="output">The output to add the element to</param>
		/// <returns>True if all strings are parsed correctly, false otherwise</returns>
		private static bool CreateNumericElement<T>(SimileNumericType type, string min, string max, string[] values, string precision, SimileLineOutput output)
			where T : struct, IComparable
		{
			try
			{
				SimileOutputNumeric<T> num = new SimileOutputNumeric<T>(type);
				if (min != null) num.Minimum = (T)Convert.ChangeType(min, typeof(T));
				if (max != null) num.Maximum = (T)Convert.ChangeType(max, typeof(T));
				if (values != null) foreach (string v in values) num.ValueSet.Add((T)Convert.ChangeType(v.Trim(), typeof(T)));
				if (precision != null)
				{
					num.Precision = int.Parse(precision);

					// Precision is limited to what Math.Round() accepts
					if (num.Precision < 0 || num.Precision > 15)
						return false;
				}

				// Add the element to the output line
				output.AddNumericElement(num);
				return true;
			}
			catch 
			{
				// One of the casts failed, so the numeric element is invalid
				return false; 
			}
		}


		/// <summary>
		/// Parses a numeric type from the given string.  The following are valid type strings:
		/// 
		/// b, byte, sb, sbyte
		/// us, ushort, ui, uint, ul, ulong
		/// s, short, i, int, l, long
		/// f, float, d, double
		/// c, char
		/// </summary>
		/// <param name="type">The type as a string</param>
		/// <returns>The numeric type.  If the parse fails, Unknown is returned</returns>
		private static SimileNumericType ParseType(string type)
		{
			// Verify we have a string
			if (type == null)
				return SimileNumericType.Unknown;

			// Check for known values
			switch (type.ToLower())
			{
				case "b": case "byte": return SimileNumericType.Byte;
				case "sb": case "sbyte": return SimileNumericType.SignedByte;
				case "s": case "short": return SimileNumericType.Short;
				case "us": case "ushort": return SimileNumericType.UnsignedShort;
				case "i": case "int": return SimileNumericType.Int;
				case "ui": case "uint": return SimileNumericType.UnsignedInt;
				case "l": case "long": return SimileNumericType.Long;
				case "ul": case "ulong": return SimileNumericType.UnsignedLong;
				case "f": case "float": return SimileNumericType.Float;
				case "d": case "double": return SimileNumericType.Double;
				case "c": case "char": return SimileNumericType.Char;
				default: return SimileNumericType.Unknown;
			}
		}
	}

	/// <summary>
	/// Represents a problem during a simile parse
	/// </summary>
	public class SimileParseException : Exception
	{
		/// <summary>
		/// Gets the number of the line on which the error occured
		/// </summary>
		public int LineNumber { get; }

		/// <summary>
		/// Gets the line of text that caused the error
		/// </summary>
		public string LineText { get; }

		/// <summary>
		/// Creates an exception object to represent an error during a simile parse
		/// </summary>
		/// <param name="message">The overall error message</param>
		/// <param name="line">The line from the simile that caused the error</param>
		/// <param name="lineNumber">The number of the line that caused the error</param>
		public SimileParseException(string message, string lineText = null, int lineNumber = -1)
			: base(message)
		{
			LineText = lineText;
			LineNumber = lineNumber;
		}
	}
}
