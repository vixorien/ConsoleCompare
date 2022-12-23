using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.VisualStudio.Shell.Interop;
using System.Linq.Expressions;

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
		private const string ElementTagStart = "[[";
		private const string ElementTagEnd = "]]";
		private const char ElementOptionDelimeter = ';';


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
				if (!success)
					return null;
				// Maybe throw exception?
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
				case '.':
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

				case ';':
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
				case '>': simile.AddInput(line.Substring(1)); break;

				// Comments: Simply ignore, but here if we want to do something
				case '#': break;

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

		
		private static bool ParseNumericTag(string tag, SimileLineOutput output)
		{
			// Split into options
			string[] allOptions = tag.Split(ElementOptionDelimeter);

			// Process each option
			SimileNumericType type = SimileNumericType.Unknown;
			string min = null;
			string max = null;
			string[] values = null;
			string precision = null;

			foreach (string op in allOptions)
			{
				// Split across the equals and check
				string[] pieces = op.Split('=');
				if (pieces.Length != 2)
					return false;

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
						values = pieces[1].Replace("{","").Replace("}","").Split(',');
						break;

					case "p":
					case "precision":
						precision = pieces[1];
						break;
				}
			}

			// Create the numeric type
			return CreateNumericElement(type, min, max, values, precision, output);
		}


		private static bool CreateNumericElement(SimileNumericType type, string min, string max, string[] values, string precision, SimileLineOutput output)
		{
			// A little sloppy, but this'll make the rest more concise
			try
			{
				switch (type)
				{
					case SimileNumericType.Byte:
						SimileOutputNumeric<byte> b = new SimileOutputNumeric<byte>(type);
						if (min != null) b.Minimum = byte.Parse(min);
						if (max != null) b.Maximum = byte.Parse(max);
						if (values != null) foreach (string v in values) b.ValueSet.Add(byte.Parse(v));
						if (precision != null) b.Precision = int.Parse(precision);
						output.AddNumericElement(b);
						return true;

					case SimileNumericType.SignedByte:
						SimileOutputNumeric<sbyte> sb = new SimileOutputNumeric<sbyte>(type);
						if (min != null) sb.Minimum = sbyte.Parse(min);
						if (max != null) sb.Maximum = sbyte.Parse(max);
						if (values != null) foreach (string v in values) sb.ValueSet.Add(sbyte.Parse(v));
						if (precision != null) sb.Precision = int.Parse(precision);
						output.AddNumericElement(sb);
						return true;

					case SimileNumericType.Char:
						SimileOutputNumeric<char> c = new SimileOutputNumeric<char>(type);
						if (min != null) c.Minimum = char.Parse(min);
						if (max != null) c.Maximum = char.Parse(max);
						if (values != null) foreach (string v in values) c.ValueSet.Add(char.Parse(v));
						if (precision != null) c.Precision = int.Parse(precision);
						output.AddNumericElement(c);
						return true;

					case SimileNumericType.Short:
						SimileOutputNumeric<short> s = new SimileOutputNumeric<short>(type);
						if (min != null) s.Minimum = short.Parse(min);
						if (max != null) s.Maximum = short.Parse(max);
						if (values != null) foreach (string v in values) s.ValueSet.Add(short.Parse(v));
						if (precision != null) s.Precision = int.Parse(precision);
						output.AddNumericElement(s);
						return true;

					case SimileNumericType.UnsignedShort:
						SimileOutputNumeric<ushort> us = new SimileOutputNumeric<ushort>(type);
						if (min != null) us.Minimum = ushort.Parse(min);
						if (max != null) us.Maximum = ushort.Parse(max);
						if (values != null) foreach (string v in values) us.ValueSet.Add(ushort.Parse(v));
						if (precision != null) us.Precision = int.Parse(precision);
						output.AddNumericElement(us);
						return true;

					case SimileNumericType.Int:
						SimileOutputNumeric<int> i = new SimileOutputNumeric<int>(type);
						if (min != null) i.Minimum = int.Parse(min);
						if (max != null) i.Maximum = int.Parse(max);
						if (values != null) foreach (string v in values) i.ValueSet.Add(int.Parse(v));
						if (precision != null) i.Precision = int.Parse(precision);
						output.AddNumericElement(i);
						return true;

					case SimileNumericType.UnsignedInt:
						SimileOutputNumeric<uint> ui = new SimileOutputNumeric<uint>(type);
						if (min != null) ui.Minimum = uint.Parse(min);
						if (max != null) ui.Maximum = uint.Parse(max);
						if (values != null) foreach (string v in values) ui.ValueSet.Add(uint.Parse(v));
						if (precision != null) ui.Precision = int.Parse(precision);
						output.AddNumericElement(ui);
						return true;

					case SimileNumericType.Long:
						SimileOutputNumeric<long> l = new SimileOutputNumeric<long>(type);
						if (min != null) l.Minimum = long.Parse(min);
						if (max != null) l.Maximum = long.Parse(max);
						if (values != null) foreach (string v in values) l.ValueSet.Add(long.Parse(v));
						if (precision != null) l.Precision = int.Parse(precision);
						output.AddNumericElement(l);
						return true;

					case SimileNumericType.UnsignedLong:
						SimileOutputNumeric<ulong> ul = new SimileOutputNumeric<ulong>(type);
						if (min != null) ul.Minimum = ulong.Parse(min);
						if (max != null) ul.Maximum = ulong.Parse(max);
						if (values != null) foreach (string v in values) ul.ValueSet.Add(ulong.Parse(v));
						if (precision != null) ul.Precision = int.Parse(precision);
						output.AddNumericElement(ul);
						return true;

					case SimileNumericType.Float:
						SimileOutputNumeric<float> f = new SimileOutputNumeric<float>(type);
						if (min != null) f.Minimum = float.Parse(min);
						if (max != null) f.Maximum = float.Parse(max);
						if (values != null) foreach (string v in values) f.ValueSet.Add(float.Parse(v));
						if (precision != null) f.Precision = int.Parse(precision);
						output.AddNumericElement(f);
						return true;

					case SimileNumericType.Double:
						SimileOutputNumeric<double> d = new SimileOutputNumeric<double>(type);
						if (min != null) d.Minimum = double.Parse(min);
						if (max != null) d.Maximum = double.Parse(max);
						if (values != null) foreach (string v in values) d.ValueSet.Add(double.Parse(v));
						if (precision != null) d.Precision = int.Parse(precision);
						output.AddNumericElement(d);
						return true;


					// Invalid type!
					case SimileNumericType.Unknown:
					default:
						return false;
				}
			}
			catch // Any problems --> return false
			{
				return false;
			}
		}

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
}
