﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Open.Database.Extensions
{
	/// <summary>
	/// Core non-DB-specific extensions for building a command and retrieving data using best practices.
	/// </summary>
	public static partial class Extensions
	{
		internal static bool IsStillAlive(this Task task)
		{
			return !task.IsCompleted && !task.IsCanceled && !task.IsFaulted;
		}

		internal static bool IsStillAlive<T>(this ITargetBlock<T> task)
		{
			return IsStillAlive(task.Completion);
		}

		// https://stackoverflow.com/questions/17660097/is-it-possible-to-speed-this-method-up/17669142#17669142
		internal static Action<T, object> BuildUntypedSetter<T>(this PropertyInfo propertyInfo)
		{
			var targetType = propertyInfo.DeclaringType;
			var methodInfo = propertyInfo.GetSetMethod();
			var exTarget = Expression.Parameter(targetType, "t");
			var exValue = Expression.Parameter(typeof(object), "p");
			var exBody = Expression.Call(exTarget, methodInfo,
			   Expression.Convert(exValue, propertyInfo.PropertyType));
			var lambda = Expression.Lambda<Action<T, object>>(exBody, exTarget, exValue);
			var action = lambda.Compile();
			return action;
		}

		internal static object DBNullValueToNull(object value)
			=> value == DBNull.Value ? null : value;

		/// <summary>
		/// Any DBNull values are converted to null.
		/// </summary>
		/// <param name="values">The source values.</param>
		/// <returns>The converted enumerable.</returns>
		public static IEnumerable<object> DBNullToNull(this IEnumerable<object> values)
		{
			foreach (var v in values)
				yield return DBNullValueToNull(v);
		}

		/// <summary>
		/// Returns a copy of this array with any DBNull values converted to null.
		/// </summary>
		/// <param name="values">The source values.</param>
		/// <returns>A new array containing the results with.</returns>
		public static object[] DBNullToNull(this object[] values)
		{
			var len = values.Length;
			var result = new object[len];
			for (var i=0;i < len; i++)
			{
				result[i] = DBNullValueToNull(values[i]);
			}
			return result;
		}

		/// <summary>
		/// Replaces any DBNull values in the array with null;
		/// </summary>
		/// <param name="values">The source values.</param>
		/// <returns>The converted enumerable.</returns>
		public static object[] ReplaceDBNullWithNull(this object[] values)
		{
			var len = values.Length;
			for (var i = 0; i < len; i++)
			{
				var value = values[i];
				if (value == DBNull.Value) values[i] = null;
			}
			return values;
		}

		/// <summary>
		/// Shortcut for creating an IDbCommand from any IDbConnection.
		/// </summary>
		/// <param name="connection">The connection to create a command from.</param>
		/// <param name="type">The command type.  Text, StoredProcedure, or TableDirect.</param>
		/// <param name="commandText">The command text or stored procedure name to use.</param>
		/// <param name="secondsTimeout">The number of seconds to wait before the command times out.</param>
		/// <returns>The created SqlCommand.</returns>
		public static IDbCommand CreateCommand(this IDbConnection connection,
			CommandType type, string commandText, int secondsTimeout = CommandTimeout.DEFAULT_SECONDS)
		{
			var command = connection.CreateCommand();
			command.CommandType = type;
			command.CommandText = commandText;
			command.CommandTimeout = secondsTimeout;

			return command;
		}

		/// <summary>
		/// Shortcut for creating an IDbCommand from any IDbConnection.
		/// </summary>
		/// <param name="connection">The connection to create a command from.</param>
		/// <param name="commandText">The command text or stored procedure name to use.</param>
		/// <param name="secondsTimeout">The number of seconds to wait before the command times out.</param>
		/// <returns>The created SqlCommand.</returns>
		public static IDbCommand CreateStoredProcedureCommand(this IDbConnection connection,
			string commandText, int secondsTimeout = CommandTimeout.DEFAULT_SECONDS)
			=> connection.CreateCommand(CommandType.StoredProcedure, commandText, secondsTimeout);

		/// <summary>
		/// Shortcut for creating an DbCommand from any DbConnection.
		/// </summary>
		/// <param name="connection">The connection to create a command from.</param>
		/// <param name="type">The command type.  Text, StoredProcedure, or TableDirect.</param>
		/// <param name="commandText">The command text or stored procedure name to use.</param>
		/// <param name="secondsTimeout">The number of seconds to wait before the command times out.</param>
		/// <returns>The created SqlCommand.</returns>
		public static DbCommand CreateCommand(this DbConnection connection,
			CommandType type, string commandText, int secondsTimeout = CommandTimeout.DEFAULT_SECONDS)
		{
			var command = connection.CreateCommand();
			command.CommandType = type;
			command.CommandText = commandText;
			command.CommandTimeout = secondsTimeout;

			return command;
		}

		/// <summary>
		/// Shortcut for creating an DbCommand from any DbConnection.
		/// </summary>
		/// <param name="connection">The connection to create a command from.</param>
		/// <param name="commandText">The command text or stored procedure name to use.</param>
		/// <param name="secondsTimeout">The number of seconds to wait before the command times out.</param>
		/// <returns>The created SqlCommand.</returns>
		public static DbCommand CreateStoredProcedureCommand(this DbConnection connection,
			string commandText, int secondsTimeout = CommandTimeout.DEFAULT_SECONDS)
			=> connection.CreateCommand(CommandType.StoredProcedure, commandText, secondsTimeout);

		/// <summary>
		/// Shortcut for adding command parameter.
		/// </summary>
		/// <param name="target">The command to add a parameter to.</param>
		/// <param name="name">The name of the parameter.</param>
		/// <param name="value">The value of the parameter.</param>
		/// <returns>The created IDbDataParameter.</returns>
		public static IDbDataParameter AddParameter(this IDbCommand target,
			string name, object value = null)
		{
			var c = target.CreateParameter();
			c.ParameterName = name;
			if (value != null) // DBNull.Value is allowed.
				c.Value = value;
			target.Parameters.Add(c);
			return c;
		}

		/// <summary>
		/// Shortcut for adding command parameter.
		/// </summary>
		/// <param name="target">The command to add a parameter to.</param>
		/// <param name="name">The name of the parameter.</param>
		/// <param name="value">The value of the parameter.</param>
		/// <param name="type">The DbType of the parameter.</param>
		/// <param name="direction">The direction of the parameter.</param>
		/// <returns>The created IDbDataParameter.</returns>
		public static IDbDataParameter AddParameter(this IDbCommand target,
			string name, object value, DbType type, ParameterDirection direction = ParameterDirection.Input)
		{
			var p = target.AddParameterType(name, type);
			p.Value = value;
			return p;
		}

		/// <summary>
		/// Shortcut for adding command a typed (non-input) parameter.
		/// </summary>
		/// <param name="target">The command to add a parameter to.</param>
		/// <param name="name">The name of the parameter.</param>
		/// <param name="type">The DbType of the parameter.</param>
		/// <param name="direction">The direction of the parameter.</param>
		/// <returns>The created IDbDataParameter.</returns>
		public static IDbDataParameter AddParameterType(this IDbCommand target,
			string name, DbType type, ParameterDirection direction = ParameterDirection.Input)
		{
			var c = target.CreateParameter();
			c.ParameterName = name;
			c.DbType = type;
			c.Direction = direction;
			target.Parameters.Add(c);
			return c;
		}


		/// <summary>
		/// Iterates all records from an IDataReader.
		/// </summary>
		/// <param name="reader">The IDataReader to iterate.</param>
		/// <param name="handler">The handler function for each IDataRecord.</param>
		public static void ForEach(this IDataReader reader, Action<IDataRecord> handler)
		{
			while (reader.Read()) handler(reader);
		}

		/// <summary>
		/// Iterates all records from an IDataReader.
		/// </summary>
		/// <param name="reader">The IDataReader to iterate.</param>
		/// <param name="handler">The handler function for each IDataRecord.</param>
		public static async Task ForEachAsync(this DbDataReader reader, Action<IDataRecord> handler)
		{
			while (await reader.ReadAsync()) handler(reader);
		}

		/// <summary>
		/// Iterates all records from an IDataReader.
		/// </summary>
		/// <param name="reader">The IDataReader to iterate.</param>
		/// <param name="handler">The handler function for each IDataRecord.</param>
		public static async Task ForEachAsync(this DbDataReader reader, Func<IDataRecord, Task> handler)
		{
			while (await reader.ReadAsync()) await handler(reader);
		}

		/// <summary>
		/// Returns all the column names for the current result set.
		/// </summary>
		/// <param name="record">The reader to get column names from.</param>
		/// <returns>The array of column names.</returns>
		public static string[] GetNames(this IDataRecord record)
		{
			var fieldCount = record.FieldCount;
			var columnNames = new string[fieldCount];
			for (var i = 0; i < fieldCount; i++)
				columnNames[i] = record.GetName(i);
			return columnNames;
		}

		/// <summary>
		/// Returns the (name,ordinal) mapping for current result set.
		/// </summary>
		/// <param name="record">The reader to get column names from.</param>
		/// <returns>The array of mappings.</returns>
		public static (string Name, int Ordinal)[] GetOrdinalMapping(this IDataRecord record)
			=> record.GetNames().Select((n, o) => (Name: n, Ordinal: o)).ToArray();


		/// <summary>
		/// Returns an array of name to ordinal mappings.
		/// </summary>
		/// <param name="record">The IDataRecord to query the ordinals from.</param>
		/// <param name="columnNames">The requested column names.</param>
		/// <param name="sort">If true, will order the results by ordinal ascending.</param>
		/// <returns></returns>
		public static (string Name, int Ordinal)[] GetOrdinalMapping(this IDataRecord record, IEnumerable<string> columnNames, bool sort = false)
		{
			if (columnNames == null) throw new ArgumentNullException(nameof(columnNames));
			var cn = columnNames as string[] ?? columnNames.ToArray();
			if (cn.Length == 0) return Array.Empty<(string Name, int Ordinal)>();
			try
			{
				var q = cn
					.Select(n => (Name: n, Ordinal: record.GetOrdinal(n))); // Does do a case-insensitive search after a case-sensitive one.

				if (sort)
					q = q.OrderBy(m => m.Ordinal);

				return q.ToArray();

			}
			catch (IndexOutOfRangeException iorex)
			{
				var mismatch = new HashSet<string>(cn);
				mismatch.ExceptWith(record.GetNames());

				// Columns not mapped correctly.  Report all columns that are mismatched/missing.
				throw new IndexOutOfRangeException($"Invalid columns: {String.Join(", ", mismatch.OrderBy(c => c).ToArray())}", iorex);
			}
		}

		/// <summary>
		/// Produces a selective set of column values based upon the desired ordinal positions.
		/// </summary>
		/// <param name="record">The IDataRecord to query.</param>
		/// <param name="ordinals">The set of ordinals to query.</param>
		/// <returns>An array of values matching the ordinal positions requested.</returns>
		public static object[] GetValuesFromOrdinals(this IDataRecord record, params int[] ordinals)
		{
			var count = ordinals.Length;
			if (count == 0) return Array.Empty<object>();

			var values = new object[count];
			for (var i = 0; i < count; i++)
				values[i] = record.GetValue(ordinals[i]);
			return values;
		}


		/// <summary>
		/// Returns an array of name to ordinal mappings.
		/// </summary>
		/// <param name="record">The IDataRecord to query the ordinals from.</param>
		/// <param name="columnNames">The requested column names.</param>
		/// <param name="sort">If true, will order the results by ordinal ascending.</param>
		/// <returns></returns>
		public static (string Name, int Ordinal)[] GetMatchingOrdinals(this IDataRecord record, IEnumerable<string> columnNames, bool sort = false)
		{
			if (columnNames == null) throw new ArgumentNullException(nameof(columnNames));

			// Normalize the requested column names to be lowercase.
			columnNames = columnNames.Select(c =>
			{
				if (string.IsNullOrWhiteSpace(c))
					throw new ArgumentException("Column names cannot be null or whitespace only.");
				return c.ToLowerInvariant();
			});

			var actual = record.GetOrdinalMapping();
			if (sort)
			{
				var requested = new HashSet<string>(columnNames);
				// Return actual values based upon if their lower-case counterparts exist in the requested.
				return actual
					.Where(m => columnNames.Contains(m.Name.ToLowerInvariant()))
					.ToArray();
			}
			else
			{
				// Create a map of lower-case keys to acutal.
				var actualColumns = actual.ToDictionary(m => m.Name.ToLowerInvariant(), m => m);
				return columnNames
					.Where(c => actualColumns.ContainsKey(c)) // Select lower case column names if they exist in the dictionary.
					.Select(c => actualColumns[c]) // Then select the actual values based upon that key.
					.ToArray();
			}
		}

		/// <summary>
		/// Returns all the data type names for the columns of current result set.
		/// </summary>
		/// <param name="reader">The reader to get data type names from.</param>
		/// <returns>The array of data type names.</returns>
		public static string[] GetDataTypeNames(this IDataRecord reader)
		{
			var fieldCount = reader.FieldCount;
			var results = new string[fieldCount];
			for (var i = 0; i < fieldCount; i++)
				results[i] = reader.GetDataTypeName(i);
			return results;
		}

		/// <summary>
		/// Enumerates all the remaining values of the current result set of a data reader.
		/// DBNull values are retained.
		/// </summary>
		/// <param name="reader">The reader to enumerate.</param>
		/// <returns>An enumeration of the values returned from a data reader.</returns>
		public static IEnumerable<object[]> AsEnumerable(this IDataReader reader)
		{
			if (reader.Read())
			{
				var fieldCount = reader.FieldCount;
				do
				{
					var row = new object[fieldCount];
					reader.GetValues(row);
					yield return row;
				} while (reader.Read());
			}
		}


		static IEnumerable<object[]> AsEnumerableInternal(this IDataReader reader, IEnumerable<int> ordinals, bool readStarted)
		{
			if (readStarted || reader.Read())
			{
				var o = ordinals as int[] ?? ordinals.ToArray();
				var fieldCount = o.Length;
				if (fieldCount == 0)
				{
					do
					{
						yield return Array.Empty<object>();
					} while (reader.Read());
				}
				else
				{
					do
					{
						var row = new object[fieldCount];
						for (var i = 0; i < fieldCount; i++)
							row[i] = reader.GetValue(o[i]);
						yield return row;
					} while (reader.Read());
				}

			}
		}

		/// <summary>
		/// Enumerates all the remaining values of the current result set of a data reader.
		/// DBNull values are retained.
		/// </summary>
		/// <param name="reader">The reader to enumerate.</param>
		/// <param name="ordinals">The limited set of ordinals to include.  If none are specified, the returned objects will be empty.</param>
		/// <returns>An enumeration of the values returned from a data reader.</returns>
		public static IEnumerable<object[]> AsEnumerable(this IDataReader reader, IEnumerable<int> ordinals)
			=> AsEnumerableInternal(reader, ordinals, false);

		/// <summary>
		/// Enumerates all the remaining values of the current result set of a data reader.
		/// </summary>
		/// <param name="reader">The reader to enumerate.</param>
		/// <param name="n">The first ordinal to include in the request to the reader for each record.</param>
		/// <param name="others">The remaining ordinals to request from the reader for each record.</param>
		/// <returns>An enumeration of the values returned from a data reader.</returns>
		public static IEnumerable<object[]> AsEnumerable(this IDataReader reader, int n, params int[] others)
			=> AsEnumerable(reader, new int[1] { n }.Concat(others));

		/// <summary>
		/// Generic enumerable extension for DataColumnCollection.
		/// </summary>
		/// <param name="columns">The column collection.</param>
		/// <returns>An enumerable of DataColumns.</returns>
		public static IEnumerable<DataColumn> AsEnumerable(this DataColumnCollection columns)
		{
			foreach (DataColumn c in columns)
				yield return c;
		}

		/// <summary>
		/// Generic enumerable extension for DataRowCollection.
		/// </summary>
		/// <param name="rows">The row collection.</param>
		/// <returns>An enumerable of DataRows.</returns>
		public static IEnumerable<DataRow> AsEnumerable(this DataRowCollection rows)
		{
			foreach (DataRow r in rows)
				yield return r;
		}


		/// <summary>
		/// Loads all data into a queue before iterating (dequeing) the results as type T.
		/// </summary>
		/// <typeparam name="T">The model type to map the values to (using reflection).</typeparam>
		/// <param name="table">The DataTable to read from.</param>
		/// <param name="fieldMappingOverrides">An optional override map of field names to column names where the keys are the property names, and values are the column names.</param>
		/// <param name="clearSourceTable">Clears the source table before providing the enumeration.</param>
		/// <returns>An enumerable used to iterate the results.</returns>
		public static IEnumerable<T> To<T>(this DataTable table, IEnumerable<(string Field, string Column)> fieldMappingOverrides, bool clearSourceTable = false) where T : new()
		{
			var x = new Transformer<T>(fieldMappingOverrides);
			return x.Results(table, clearSourceTable);
		}

		/// <summary>
		/// Loads all data into a queue before iterating (dequeing) the results as type T.
		/// </summary>
		/// <typeparam name="T">The model type to map the values to (using reflection).</typeparam>
		/// <param name="table">The DataTable to read from.</param>
		/// <param name="fieldMappingOverrides">An optional override map of field names to column names where the keys are the property names, and values are the column names.</param>
		/// <returns>An enumerable used to iterate the results.</returns>
		public static IEnumerable<T> To<T>(this DataTable table, params (string Field, string Column)[] fieldMappingOverrides) where T : new()
		{
			var x = new Transformer<T>(fieldMappingOverrides);
			return x.Results(table, false);
		}

		/// <summary>
		/// Loads all data into a queue before iterating (dequeing) the results as type T.
		/// </summary>
		/// <typeparam name="T">The model type to map the values to (using reflection).</typeparam>
		/// <param name="table">The DataTable to read from.</param>
		/// <param name="fieldMappingOverrides">An optional override map of field names to column names where the keys are the property names, and values are the column names.</param>
		/// <param name="clearSourceTable">Clears the source table before providing the enumeration.</param>
		/// <returns>An enumerable used to iterate the results.</returns>
		public static IEnumerable<T> To<T>(this DataTable table, IEnumerable<KeyValuePair<string, string>> fieldMappingOverrides, bool clearSourceTable = false) where T : new()
			=> table.To<T>(fieldMappingOverrides?.Select(kvp => (kvp.Key, kvp.Value)));

		/// <summary>
		/// Iterates all records from an IDataReader.
		/// </summary>
		/// <typeparam name="T">The return type of the transform function.</typeparam>
		/// <param name="reader">The IDataReader to iterate.</param>
		/// <param name="transform">The transform function to process each IDataRecord.</param>
		/// <returns>An enumerable used to iterate the results.</returns>
		public static IEnumerable<T> Iterate<T>(this IDataReader reader, Func<IDataRecord, T> transform)
		{
			while (reader.Read())
				yield return transform(reader);
		}

		/// <summary>
		/// Shortcut for .Iterate(transform).ToList();
		/// </summary>
		/// <typeparam name="T">The return type of the transform function.</typeparam>
		/// <param name="reader">The IDataReader to iterate.</param>
		/// <param name="transform">The transform function to process each IDataRecord.</param>
		/// <returns>A list of the transformed results.</returns>
		public static List<T> ToList<T>(this IDataReader reader,
			Func<IDataRecord, T> transform)
			=> reader.Iterate(transform).ToList();

		/// <summary>
		/// Asynchronously iterates all records using an IDataReader and returns the desired results as a list.
		/// </summary>
		/// <typeparam name="T">The return type of the transform function.</typeparam>
		/// <param name="reader">The SqlDataReader to read from.</param>
		/// <param name="transform">The transform function to process each IDataRecord.</param>
		/// <returns>A task containing a list of all results.</returns>
		public static async Task<List<T>> ToListAsync<T>(this DbDataReader reader,
			Func<IDataRecord, T> transform)
		{
			var list = new List<T>();
			while (await reader.ReadAsync()) list.Add(transform(reader));
			return list;
		}

		/// <summary>
		/// Asynchronously iterates all records using an IDataReader and returns the desired results as a list.
		/// </summary>
		/// <typeparam name="T">The return type of the transform function.</typeparam>
		/// <param name="command">The DbCommand to generate a reader from.</param>
		/// <param name="transform">The transform function to process each IDataRecord.</param>
		/// <returns>A task containing a list of all results.</returns>
		public static async Task<List<T>> ToListAsync<T>(this DbCommand command,
			Func<IDataRecord, T> transform)
		{
			using (var reader = await command.ExecuteReaderAsync())
				return await reader.ToListAsync(transform);
		}

		/// <summary>
		/// Shortcut for .Iterate(transform).ToArray();
		/// </summary>
		/// <typeparam name="T">The return type of the transform function.</typeparam>
		/// <param name="reader">The IDataReader to iterate.</param>
		/// <param name="transform">The transform function to process each IDataRecord.</param>
		/// <returns>An array of the transformed results.</returns>
		public static T[] ToArray<T>(this IDataReader reader, Func<IDataRecord, T> transform)
			=> reader.Iterate(transform).ToArray();


		/// <summary>
		/// Iterates all records using an IDataReader and returns the desired results as a list.
		/// </summary>
		/// <typeparam name="T">The return type of the transform function.</typeparam>
		/// <param name="command">The IDbCommand to generate a reader from.</param>
		/// <param name="transform">The transform function to process each IDataRecord.</param>
		/// <returns>A list of all results.</returns>
		public static List<T> ToList<T>(this IDbCommand command, Func<IDataRecord, T> transform)
		{
			using (var reader = command.ExecuteReader())
				return reader.Iterate(transform).ToList();
		}

		/// <summary>
		/// Iterates all records using an IDataReader and returns the desired results as a list.
		/// </summary>
		/// <typeparam name="T">The return type of the transform function.</typeparam>
		/// <param name="command">The IDbCommand to generate a reader from.</param>
		/// <param name="behavior">The command behavior for once the command the reader is complete.</param>
		/// <param name="transform">The transform function to process each IDataRecord.</param>
		/// <returns>A list of all results.</returns>
		public static List<T> ToList<T>(this IDbCommand command, CommandBehavior behavior, Func<IDataRecord, T> transform)
		{
			using (var reader = command.ExecuteReader(behavior))
				return reader.Iterate(transform).ToList();
		}

		/// <summary>
		/// Iterates all records using an IDataReader and returns the desired results as a list.
		/// </summary>
		/// <typeparam name="T">The return type of the transform function.</typeparam>
		/// <param name="command">The IDbCommand to generate a reader from.</param>
		/// <param name="transform">The transform function to process each IDataRecord.</param>
		/// <returns>A list of all results.</returns>
		public static T[] ToArray<T>(this IDbCommand command, Func<IDataRecord, T> transform)
		{
			using (var reader = command.ExecuteReader())
				return reader.Iterate(transform).ToArray();
		}

		/// <summary>
		/// Iterates all records using an IDataReader and returns the desired results as a list.
		/// </summary>
		/// <typeparam name="T">The return type of the transform function.</typeparam>
		/// <param name="command">The IDbCommand to generate a reader from.</param>
		/// <param name="behavior">The command behavior for once the command the reader is complete.</param>
		/// <param name="transform">The transform function to process each IDataRecord.</param>
		/// <returns>A list of all results.</returns>
		public static T[] ToArray<T>(this IDbCommand command, CommandBehavior behavior, Func<IDataRecord, T> transform)
		{
			using (var reader = command.ExecuteReader(behavior))
				return reader.Iterate(transform).ToArray();
		}

		/// <summary>
		/// Loads all remaining data from an IDataReader into a DataTable.
		/// </summary>
		/// <param name="reader">The IDataReader to load data from.</param>
		/// <returns>The resultant DataTable.</returns>
		public static DataTable ToDataTable(this IDataReader reader)
		{
			var table = new DataTable();
			table.Load(reader);
			return table;

		}

		/// <summary>
		/// Loads all data from a command through an IDataReader into a DataTable.
		/// </summary>
		/// <param name="command">The IDbCommand to generate a reader from.</param>
		/// <param name="behavior">The command behavior for once the command the reader is complete.</param>
		/// <returns>The resultant DataTable.</returns>
		public static DataTable ToDataTable(this IDbCommand command, CommandBehavior behavior = CommandBehavior.SequentialAccess)
		{
			using (var reader = command.ExecuteReader(behavior))
				return reader.ToDataTable();
		}

		/// <summary>
		/// Loads all data from a command through an IDataReader into a DataTables.
		/// Calls .NextResult() to check for more results.
		/// </summary>
		/// <param name="command">The IDbCommand to generate a reader from.</param>
		/// <param name="behavior">The command behavior for once the command the reader is complete.</param>
		/// <returns>The resultant list of DataTables.</returns>
		public static List<DataTable> ToDataTables(this IDbCommand command, CommandBehavior behavior = CommandBehavior.SequentialAccess)
		{
			var results = new List<DataTable>();
			using (var reader = command.ExecuteReader(behavior))
			{
				do
				{
					results.Add(reader.ToDataTable());
				}
				while (reader.NextResult());
			}
			return results;
		}

		/// <summary>
		/// Iterates an IDataReader while the predicate returns true.
		/// </summary>
		/// <param name="reader">The IDataReader to iterate.</param>
		/// <param name="predicate">The handler function that processes each IDataRecord and decides if iteration should continue.</param>
		public static void IterateWhile(this IDataReader reader, Func<IDataRecord, bool> predicate)
		{
			while (reader.Read() && predicate(reader)) { }
		}

		/// <summary>
		/// Executes a reader on a command with a handler function.
		/// </summary>
		/// <param name="command">The IDbCommand to generate a reader from.</param>
		/// <param name="handler">The handler function for each IDataRecord.</param>
		/// <param name="behavior">The command behavior for once the command the reader is complete.</param>
		public static void ExecuteReader(this IDbCommand command, Action<IDataReader> handler, CommandBehavior behavior = CommandBehavior.Default)
		{
			using (var reader = command.ExecuteReader(behavior))
				handler(reader);
		}

		/// <summary>
		/// Executes a reader on a command with a transform function.
		/// </summary>
		/// <typeparam name="T">The return type of the transform function.</typeparam>
		/// <param name="command">The IDbCommand to generate a reader from.</param>
		/// <param name="transform">The transform function for each IDataRecord.</param>
		/// <param name="behavior">The command behavior for once the command the reader is complete.</param>
		/// <returns>The result of the transform.</returns>
		public static T ExecuteReader<T>(this IDbCommand command, Func<IDataReader, T> transform, CommandBehavior behavior = CommandBehavior.Default)
		{
			using (var reader = command.ExecuteReader(behavior))
				return transform(reader);
		}

		/// <summary>
		/// Executes a reader on a command with a transform function.
		/// </summary>
		/// <typeparam name="TEntity">The return type of the transform function applied to each record.</typeparam>
		/// <typeparam name="TResult">The type returned by the selector.</typeparam>
		/// <param name="command">The IDbCommand to generate a reader from.</param>
		/// <param name="transform">The transform function for each IDataRecord.</param>
		/// <param name="selector">Provides an IEnumerable&lt;TEntity&gt; to select individual results by.</param>
		/// <returns>The result of the transform.</returns>
		public static TResult IterateReader<TEntity, TResult>(
			this IDbCommand command,
			Func<IDataRecord, TEntity> transform,
			Func<IEnumerable<TEntity>, TResult> selector)
		{
			using (var reader = command.ExecuteReader())
				return selector(reader.Iterate(transform));
		}

		/// <summary>
		/// Executes a reader on a command with a transform function.
		/// </summary>
		/// <typeparam name="TEntity">The return type of the transform function applied to each record.</typeparam>
		/// <typeparam name="TResult">The type returned by the selector.</typeparam>
		/// <param name="command">The IDbCommand to generate a reader from.</param>
		/// <param name="behavior">The command behavior for once the command the reader is complete.</param>
		/// <param name="transform">The transform function for each IDataRecord.</param>
		/// <param name="selector">Provides an IEnumerable&lt;TEntity&gt; to select individual results by.</param>
		/// <returns>The result of the transform.</returns>
		public static TResult IterateReader<TEntity, TResult>(
			this IDbCommand command,
			CommandBehavior behavior,
			Func<IDataRecord, TEntity> transform,
			Func<IEnumerable<TEntity>, TResult> selector)
		{
			using (var reader = command.ExecuteReader(behavior))
				return selector(reader.Iterate(transform));
		}

		/// <summary>
		/// Iterates a reader on a command with a handler function.
		/// </summary>
		/// <param name="command">The IDbCommand to generate a reader from.</param>
		/// <param name="handler">The handler function for each IDataRecord.</param>
		public static void IterateReader(this IDbCommand command, Action<IDataRecord> handler)
		{
			using (var reader = command.ExecuteReader())
				reader.ForEach(handler);
		}

		/// <summary>
		/// Iterates a reader on a command with a handler function.
		/// </summary>
		/// <param name="command">The IDbCommand to generate a reader from.</param>
		/// <param name="behavior">The command behavior for once the command the reader is complete.</param>
		/// <param name="handler">The handler function for each IDataRecord.</param>
		public static void IterateReader(this IDbCommand command, CommandBehavior behavior, Action<IDataRecord> handler)
		{
			using (var reader = command.ExecuteReader(behavior))
				reader.ForEach(handler);
		}

		internal static IEnumerable<T> IterateReaderInternal<T>(IDbCommand command, CommandBehavior behavior, Func<IDataRecord, T> transform)
		{
			using (var reader = command.ExecuteReader(behavior))
			{
				while (reader.Read())
					yield return transform(reader);
			}
		}

		internal static IEnumerable<object[]> IterateReaderInternal(IDbCommand command, CommandBehavior behavior = CommandBehavior.Default)
		{
			using (var reader = command.ExecuteReader(behavior))
			{
				if (reader.Read())
				{
					var fieldCount = reader.FieldCount;
					do
					{
						var row = new object[fieldCount];
						reader.GetValues(row);
						yield return row;
					} while (reader.Read());
				}
			}
		}

		/// <summary>
		/// Iterates an IDataReader on a command while the predicate returns true.
		/// </summary>
		/// <param name="command">The IDbCommand to generate a reader from.</param>
		/// <param name="predicate">The handler function that processes each IDataRecord and decides if iteration should continue.</param>
		/// <param name="behavior">The command behavior for once the command the reader is complete.</param>
		public static void IterateReaderWhile(this IDbCommand command, Func<IDataRecord, bool> predicate, CommandBehavior behavior = CommandBehavior.Default)
		{
			using (var reader = command.ExecuteReader(behavior))
				reader.IterateWhile(predicate);
		}

		/// <summary>
		/// Asynchronously iterates all records from an IDataReader.
		/// </summary>
		/// <param name="command">The DbCommand to generate a reader from.</param>
		/// <param name="handler">The handler function for each IDataRecord.</param>
		/// <param name="token">Optional cancellatio token.</param>
		public static async Task ForEachAsync(this DbCommand command, Action<IDataRecord> handler, CancellationToken? token = null)
		{
			using (var reader = await command.ExecuteReaderAsync())
			{
				if (token.HasValue)
				{
					var t = token.Value;
					while (!t.IsCancellationRequested && await reader.ReadAsync())
						handler(reader);
				}
				else
				{
					while (await reader.ReadAsync())
						handler(reader);
				}
			}
		}

		/// <summary>
		/// Asynchronously iterates an IDataReader on a command while the predicate returns true.
		/// </summary>
		/// <param name="command">The DbCommand to generate a reader from.</param>
		/// <param name="predicate">The handler function that processes each IDataRecord and decides if iteration should continue.</param>
		/// <param name="behavior">The command behavior for once the command the reader is complete.</param>
		public static async Task IterateReaderAsyncWhile(this DbCommand command, Func<IDataRecord, bool> predicate, CommandBehavior behavior = CommandBehavior.Default)
		{
			using (var reader = await command.ExecuteReaderAsync(behavior))
				while (await reader.ReadAsync() && predicate(reader)) { }
		}

		/// <summary>
		/// Asynchronously iterates an IDataReader on a command while the predicate returns true.
		/// </summary>
		/// <param name="command">The DbCommand to generate a reader from.</param>
		/// <param name="predicate">The handler function that processes each IDataRecord and decides if iteration should continue.</param>
		/// <param name="behavior">The command behavior for once the command the reader is complete.</param>
		public static async Task IterateReaderAsyncWhile(this DbCommand command, Func<IDataRecord, Task<bool>> predicate, CommandBehavior behavior = CommandBehavior.Default)
		{
			using (var reader = await command.ExecuteReaderAsync(behavior))
				while (await reader.ReadAsync() && await predicate(reader)) { }
		}

		/// <summary>
		/// Iterates an IDataReader and returns the first result through a transform funciton.  Throws if none.
		/// </summary>
		/// <typeparam name="T">The return type of the transform function.</typeparam>
		/// <param name="command">The IDbCommand to generate a reader from.</param>
		/// <param name="transform">The transform function to process each IDataRecord.</param>
		/// <returns>The value from the transform.</returns>
		public static T First<T>(this IDbCommand command, Func<IDataRecord, T> transform)
		{
			using (var reader = command.ExecuteReader(CommandBehavior.SingleRow))
				return reader.Iterate(transform).First();
		}

		/// <summary>
		/// Iterates an IDataReader and returns the first result through a transform funciton.  Returns default(T) if none.
		/// </summary>
		/// <typeparam name="T">The return type of the transform function.</typeparam>
		/// <param name="command">The IDbCommand to generate a reader from.</param>
		/// <param name="transform">The transform function to process each IDataRecord.</param>
		/// <returns>The value from the transform.</returns>
		public static T FirstOrDefault<T>(this IDbCommand command, Func<IDataRecord, T> transform)
		{
			using (var reader = command.ExecuteReader(CommandBehavior.SingleRow))
				return reader.Iterate(transform).FirstOrDefault();
		}

		/// <summary>
		/// Iterates an IDataReader and returns the first result through a transform funciton.  Throws if none or more than one entry.
		/// </summary>
		/// <typeparam name="T">The return type of the transform function.</typeparam>
		/// <param name="command">The IDbCommand to generate a reader from.</param>
		/// <param name="transform">The transform function to process each IDataRecord.</param>
		/// <returns>The value from the transform.</returns>
		public static T Single<T>(this IDbCommand command, Func<IDataRecord, T> transform)
		{
			using (var reader = command.ExecuteReader())
				return reader.Iterate(transform).Single();
		}

		/// <summary>
		/// Iterates an IDataReader and returns the first result through a transform funciton.  Returns default(T) if none.  Throws if more than one entry.
		/// </summary>
		/// <typeparam name="T">The return type of the transform function.</typeparam>
		/// <param name="command">The IDbCommand to generate a reader from.</param>
		/// <param name="transform">The transform function to process each IDataRecord.</param>
		/// <returns>The value from the transform.</returns>
		public static T SingleOrDefault<T>(this IDbCommand command, Func<IDataRecord, T> transform)
		{
			using (var reader = command.ExecuteReader())
				return reader.Iterate(transform).SingleOrDefault();
		}

		/// <summary>
		/// Iterates an IDataReader and returns the first number of results defined by the count.
		/// </summary>
		/// <typeparam name="T">The return type of the transform function.</typeparam>
		/// <param name="command">The IDbCommand to generate a reader from.</param>
		/// <param name="count">The maximum number of records to return.</param>
		/// <param name="transform">The transform function to process each IDataRecord.</param>
		/// <returns>The results from the transform limited by the take count.</returns>
		public static List<T> Take<T>(this IDbCommand command, int count, Func<IDataRecord, T> transform)
		{
			using (var reader = command.ExecuteReader())
				return reader.Iterate(transform).Take(count).ToList();
		}

		/// <summary>
		/// Iterates an IDataReader and skips the first number of results defined by the count.
		/// </summary>
		/// <typeparam name="T">The return type of the transform function.</typeparam>
		/// <param name="command">The IDbCommand to generate a reader from.</param>
		/// <param name="count">The number of records to skip.</param>
		/// <param name="transform">The transform function to process each IDataRecord.</param>
		/// <returns>The results from the transform after the skip count.</returns>
		public static List<T> Skip<T>(this IDbCommand command, int count, Func<IDataRecord, T> transform)
		{
			using (var reader = command.ExecuteReader())
			{
				while (0 < count--) reader.Read();
				return reader.Iterate(transform).ToList();
			}
		}

		/// <summary>
		/// Iterates an IDataReader and skips by the skip parameter returns the maximum remaining defined by the take parameter.
		/// </summary>
		/// <typeparam name="T">The return type of the transform function.</typeparam>
		/// <param name="command">The IDbCommand to generate a reader from.</param>
		/// <param name="skip">The number of entries to skip before starting to take results.</param>
		/// <param name="take">The maximum number of records to return.</param>
		/// <param name="transform">The transform function to process each IDataRecord.</param>
		/// <returns>The results from the skip, transform and take operation.</returns>
		public static List<T> SkipThenTake<T>(this IDbCommand command, int skip, int take, Func<IDataRecord, T> transform)
		{
			using (var reader = command.ExecuteReader())
			{
				while (0 < skip--) reader.Read();
				return reader.Iterate(transform).Take(take).ToList();
			}
		}

		/// <summary>
		/// Returns the specified column data of IDataRecord as a Dictionary.
		/// DBNull values are converted to null.
		/// </summary>
		/// <param name="record">The IDataRecord to extract values from.</param>
		/// <param name="columnMap">The column ids and resultant names to query.</param>
		/// <returns>The resultant Dictionary of values.</returns>
		public static Dictionary<string, object> ToDictionary(this IDataRecord record, IEnumerable<KeyValuePair<int, string>> columnMap)
		{
			var e = new Dictionary<string, object>();
			if (columnMap != null)
			{
				foreach (var c in columnMap)
					e.Add(c.Value, DBNullValueToNull(record.GetValue(c.Key)));
			}
			return e;
		}

		/// <summary>
		/// Returns the specified column data of IDataRecord as a Dictionary.
		/// DBNull values are converted to null.
		/// </summary>
		/// <param name="record">The IDataRecord to extract values from.</param>
		/// <param name="columnMap">The column ids and resultant names to query.</param>
		/// <returns>The resultant Dictionary of values.</returns>
		public static Dictionary<string, object> ToDictionary(this IDataRecord record, IEnumerable<(int, string)> columnMap)
		{
			var e = new Dictionary<string, object>();
			if (columnMap != null)
			{
				foreach (var c in columnMap)
					e.Add(c.Item2, DBNullValueToNull(record.GetValue(c.Item1)));
			}
			return e;
		}

		/// <summary>
		/// Returns the specified column data of IDataRecord as a Dictionary.
		/// DBNull values are converted to null.
		/// </summary>
		/// <param name="record">The IDataRecord to extract values from.</param>
		/// <param name="columnNames">The column names to query.</param>
		/// <returns>The resultant Dictionary of values.</returns>
		public static Dictionary<string, object> ToDictionary(this IDataRecord record, ISet<string> columnNames)
		{
			var e = new Dictionary<string, object>();
			if (columnNames != null && columnNames.Count != 0)
			{
				for (var i = 0; i < record.FieldCount; i++)
				{
					var n = record.GetName(i);
					if (columnNames.Contains(n))
						e.Add(n, DBNullValueToNull(record.GetValue(i)));
				}
			}
			return e;
		}

		/// <summary>
		/// Returns the specified column data of IDataRecord as a Dictionary.
		/// DBNull values are converted to null.
		/// </summary>
		/// <param name="record">The IDataRecord to extract values from.</param>
		/// <param name="columnNames">The column names to query.  If none specified, the result will contain all columns.</param>
		/// <returns>The resultant Dictionary of values.</returns>
		public static Dictionary<string, object> ToDictionary(this IDataRecord record, params string[] columnNames)
		{
			if (columnNames.Length != 0)
				return ToDictionary(record, new HashSet<string>(columnNames));

			var e = new Dictionary<string, object>();
			for (var i = 0; i < record.FieldCount; i++)
			{
				var n = record.GetName(i);
				e.Add(n, DBNullValueToNull(record.GetValue(i)));
			}
			return e;
		}

		/// <summary>
		/// Returns the specified column data of IDataRecord as a Dictionary.
		/// DBNull values are converted to null.
		/// </summary>
		/// <param name="record">The IDataRecord to extract values from.</param>
		/// <param name="columnNames">The column names to query.</param>
		/// <returns>The resultant Dictionary of values.</returns>
		public static Dictionary<string, object> ToDictionary(this IDataRecord record, IEnumerable<string> columnNames)
			=> ToDictionary(record, new HashSet<string>(columnNames));

		static QueryResult<Queue<object[]>> RetrieveInternal(this IDataReader reader, int[] ordinals, string[] columnNames = null, bool readStarted = false)
		{
			var fieldCount = ordinals.Length;
			if (columnNames == null) columnNames = ordinals.Select(n => reader.GetName(n)).ToArray();
			else if (columnNames.Length != fieldCount) throw new ArgumentException("Mismatched array lengths of ordinals and names.");

			return new QueryResult<Queue<object[]>>(
				ordinals,
				columnNames,
				new Queue<object[]>(AsEnumerableInternal(reader, ordinals, readStarted)));
		}

		/// <summary>
		/// Iterates all records within the first result set using an IDataReader and returns the results.
		/// DBNull values are left unchanged (retained).
		/// </summary>
		/// <param name="reader">The IDataReader to read results from.</param>
		/// <returns>The QueryResult that contains all the results and the column mappings.</returns>
		public static QueryResult<Queue<object[]>> Retrieve(this IDataReader reader)
		{
			var names = reader.GetNames();
			return new QueryResult<Queue<object[]>>(
				Enumerable.Range(0, names.Length).ToArray(),
				names,
				new Queue<object[]>(AsEnumerable(reader)));
		}

		/// <summary>
		/// Iterates all records within the current result set using an IDataReader and returns the desired results as a list of Dictionaries containing only the specified column values.
		/// DBNull values are left unchanged (retained).
		/// </summary>
		/// <param name="reader">The IDataReader to read results from.</param>
		/// <param name="ordinals">The ordinals to request from the reader for each record.</param>
		/// <returns>The QueryResult that contains all the results and the column mappings.</returns>
		public static QueryResult<Queue<object[]>> Retrieve(this IDataReader reader, IEnumerable<int> ordinals)
			=> RetrieveInternal(reader, ordinals.ToArray());

		/// <summary>
		/// Iterates all records within the current result set using an IDataReader and returns the desired results.
		/// DBNull values are left unchanged (retained).
		/// </summary>
		/// <param name="reader">The IDataReader to read results from.</param>
		/// <param name="n">The first ordinal to include in the request to the reader for each record.</param>
		/// <param name="others">The remaining ordinals to request from the reader for each record.</param>
		/// <returns>The QueryResult that contains all the results and the column mappings.</returns>
		public static QueryResult<Queue<object[]>> Retrieve(this IDataReader reader, int n, params int[] others)
			=> Retrieve(reader, new int[1] { n }.Concat(others));

		/// <summary>
		/// Iterates all records within the current result set using an IDataReader and returns the desired results.
		/// DBNull values are left unchanged (retained).
		/// </summary>
		/// <param name="reader">The IDataReader to read results from.</param>
		/// <param name="columnNames">The column names to select.</param>
		/// <returns>The QueryResult that contains all the results and the column mappings.</returns>
		public static QueryResult<Queue<object[]>> Retrieve(this IDataReader reader, IEnumerable<string> columnNames)
		{
			var columns = reader.GetOrdinalMapping(columnNames);
			var ordinalValues = columns.Select(c => c.Ordinal).ToArray();
			return RetrieveInternal(reader, ordinalValues, columns.Select(c => c.Name).ToArray());
		}

		/// <summary>
		/// Iterates all records within the current result set using an IDataReader and returns the desired results.
		/// DBNull values are left unchanged (retained).
		/// </summary>
		/// <param name="reader">The IDataReader to read results from.</param>
		/// <param name="c">The first column name to include in the request to the reader for each record.</param>
		/// <param name="others">The remaining column names to request from the reader for each record.</param>
		/// <returns>The QueryResult that contains all the results and the column mappings.</returns>
		public static QueryResult<Queue<object[]>> Retrieve(this IDataReader reader, string c, params string[] others)
			=> Retrieve(reader, new string[1] { c }.Concat(others));

		/// <summary>
		/// Iterates all records within the first result set using an IDataReader and returns the results.
		/// DBNull values are left unchanged (retained).
		/// </summary>
		/// <param name="command">The IDbCommand to generate the reader from.</param>
		/// <returns>The QueryResult that contains all the results and the column mappings.</returns>
		public static QueryResult<Queue<object[]>> Retrieve(this IDbCommand command)
			=> ExecuteReader(command, reader => reader.Retrieve());

		/// <summary>
		/// Iterates all records within the current result set using an IDataReader and returns the desired results.
		/// DBNull values are left unchanged (retained).
		/// </summary>
		/// <param name="command">The IDbCommand to generate the reader from.</param>
		/// <param name="ordinals">The ordinals to request from the reader for each record.</param>
		/// <returns>The QueryResult that contains all the results and the column mappings.</returns>
		public static QueryResult<Queue<object[]>> Retrieve(this IDbCommand command, IEnumerable<int> ordinals)
			=> ExecuteReader(command, reader => reader.Retrieve(ordinals));

		/// <summary>
		/// Iterates all records within the current result set using an IDataReader and returns the desired results.
		/// DBNull values are left unchanged (retained).
		/// </summary>
		/// <param name="command">The IDbCommand to generate the reader from.</param>
		/// <param name="n">The first ordinal to include in the request to the reader for each record.</param>
		/// <param name="others">The remaining ordinals to request from the reader for each record.</param>
		/// <returns>The QueryResult that contains all the results and the column mappings.</returns>
		public static QueryResult<Queue<object[]>> Retrieve(this IDbCommand command, int n, params int[] others)
			=> ExecuteReader(command, reader => Retrieve(reader, new int[1] { n }.Concat(others)));

		/// <summary>
		/// Iterates all records within the first result set using an IDataReader and returns the desired results as a list of Dictionaries containing only the specified column values.
		/// DBNull values are left unchanged (retained).
		/// </summary>
		/// <param name="command">The IDbCommand to generate the reader from.</param>
		/// <param name="columnNames">The column names to select.</param>
		/// <returns>The QueryResult that contains all the results and the column mappings.</returns>
		public static QueryResult<Queue<object[]>> Retrieve(this IDbCommand command, IEnumerable<string> columnNames)
			=> ExecuteReader(command, reader => reader.Retrieve(columnNames));

		/// <summary>
		/// Iterates all records within the current result set using an IDataReader and returns the desired results.
		/// DBNull values are left unchanged (retained).
		/// </summary>
		/// <param name="command">The IDbCommand to generate the reader from.</param>
		/// <param name="c">The first column name to include in the request to the reader for each record.</param>
		/// <param name="others">The remaining column names to request from the reader for each record.</param>
		/// <returns>The QueryResult that contains all the results and the column mappings.</returns>
		public static QueryResult<Queue<object[]>> Retrieve(this IDbCommand command, string c, params string[] others)
			=> ExecuteReader(command, reader => Retrieve(reader, new string[1] { c }.Concat(others)));

		/// <summary>
		/// Asynchronously enumerates all the remaining values of the current result set of a data reader.
		/// DBNull values are left unchanged (retained).
		/// </summary>
		/// <param name="reader">The reader to enumerate.</param>
		/// <returns>The QueryResult that contains a buffer block of the results and the column mappings.</returns>
		public static async Task<QueryResult<Queue<object[]>>> RetrieveAsync(this DbDataReader reader)
		{
			var fieldCount = reader.FieldCount;
			var names = reader.GetNames(); // pull before first read.
			var buffer = new Queue<object[]>();

			while (await reader.ReadAsync())
			{
				var row = new object[fieldCount];
				reader.GetValues(row);
				buffer.Enqueue(row);
			}

			return new QueryResult<Queue<object[]>>(
				Enumerable.Range(0, names.Length).ToArray(),
				names,
				buffer);
		}

		static async Task<QueryResult<Queue<object[]>>> RetrieveAsyncInternal(DbDataReader reader, int[] ordinals, string[] columnNames = null, bool readStarted = false)
		{
			var fieldCount = ordinals.Length;
			if (columnNames == null) columnNames = ordinals.Select(n => reader.GetName(n)).ToArray();
			else if (columnNames.Length != fieldCount) throw new ArgumentException("Mismatched array lengths of ordinals and names.");

			Func<IDataRecord, object[]> handler;
			if (fieldCount == 0) handler = record => Array.Empty<object>();
			else handler = record =>
			{
				var row = new object[fieldCount];
				for (var i = 0; i < fieldCount; i++)
					row[i] = reader.GetValue(ordinals[i]);
				return row;
			};

			var buffer = new Queue<object[]>();
			if (readStarted || await reader.ReadAsync())
			{
				do
				{
					buffer.Enqueue(handler(reader));
				}
				while (await reader.ReadAsync());
			}

			return new QueryResult<Queue<object[]>>(
				ordinals,
				columnNames,
				buffer);
		}

		/// <summary>
		/// Asynchronously enumerates all the remaining values of the current result set of a data reader.
		/// DBNull values are left unchanged (retained).
		/// </summary>
		/// <param name="reader">The reader to enumerate.</param>
		/// <param name="ordinals">The limited set of ordinals to include.  If none are specified, the returned objects will be empty.</param>
		/// <returns>The QueryResult that contains a buffer block of the results and the column mappings.</returns>
		public static Task<QueryResult<Queue<object[]>>> RetrieveAsync(this DbDataReader reader, IEnumerable<int> ordinals)
			=> RetrieveAsyncInternal(reader, ordinals as int[] ?? ordinals.ToArray());

		/// <summary>
		/// Asynchronously enumerates all the remaining values of the current result set of a data reader.
		/// DBNull values are left unchanged (retained).
		/// </summary>
		/// <param name="reader">The reader to enumerate.</param>
		/// <param name="n">The first ordinal to include in the request to the reader for each record.</param>
		/// <param name="others">The remaining ordinals to request from the reader for each record.</param>
		/// <returns>The QueryResult that contains a buffer block of the results and the column mappings.</returns>
		public static Task<QueryResult<Queue<object[]>>> RetrieveAsync(this DbDataReader reader, int n, params int[] others)
			=> RetrieveAsync(reader, new int[1] { n }.Concat(others));

		/// <summary>
		/// Asynchronously enumerates all records within the current result set using an IDataReader and returns the desired results.
		/// DBNull values are left unchanged (retained).
		/// </summary>
		/// <param name="reader">The IDataReader to read results from.</param>
		/// <param name="columnNames">The column names to select.</param>
		/// <param name="normalizeColumnOrder">Orders the results arrays by ordinal.</param>
		/// <returns>The QueryResult that contains all the results and the column mappings.</returns>
		public static Task<QueryResult<Queue<object[]>>> RetrieveAsync(this DbDataReader reader, IEnumerable<string> columnNames, bool normalizeColumnOrder = false)
		{
			// Validate columns first.
			var columns = reader.GetOrdinalMapping(columnNames, normalizeColumnOrder);

			return RetrieveAsyncInternal(reader,
				columns.Select(c => c.Ordinal).ToArray(),
				columns.Select(c => c.Name).ToArray());
		}

		/// <summary>
		/// Asynchronously enumerates all records within the current result set using an IDataReader and returns the desired results.
		/// DBNull values are left unchanged (retained).
		/// </summary>
		/// <param name="reader">The IDataReader to read results from.</param>
		/// <param name="c">The first column name to include in the request to the reader for each record.</param>
		/// <param name="others">The remaining column names to request from the reader for each record.</param>
		/// <returns>The QueryResult that contains all the results and the column mappings.</returns>
		public static Task<QueryResult<Queue<object[]>>> RetrieveAsync(this DbDataReader reader, string c, params string[] others)
			=> RetrieveAsync(reader, new string[1] { c }.Concat(others));

		/// <summary>
		/// Useful extension for dequeuing items from a queue.
		/// Not thread safe but queueing/dequeueing items in between items is supported.
		/// </summary>
		/// <typeparam name="T">Return type of the source queue</typeparam>
		/// <returns>An enumerable of the items contained within the queue.</returns>
		public static IEnumerable<T> DequeueEach<T>(this Queue<T> source)
		{
			while (source.Count != 0)
				yield return source.Dequeue();
		}

		/// <summary>
		/// Reads the first column values from every record.
		/// DBNull values are then converted to null.
		/// </summary>
		/// <returns>The enumerable first ordinal values.</returns>
		public static IEnumerable<object> FirstOrdinalResults(this IDataReader reader)
		{
			var results = new Queue<object>(reader.Iterate(r => r.GetValue(0)));
			return results.DequeueEach().DBNullToNull();
		}

		/// <summary>
		/// Reads the first column values from every record.
		/// Any DBNull values are then converted to null and casted to type T0;
		/// </summary>
		/// <returns>The enumerable of casted values.</returns>
		public static IEnumerable<T0> FirstOrdinalResults<T0>(this IDataReader reader)
			=> reader.FirstOrdinalResults().Cast<T0>();

		/// <summary>
		/// Reads the first column values from every record.
		/// DBNull values are then converted to null.
		/// </summary>
		/// <returns>The enumerable first ordinal values.</returns>
		public static IEnumerable<object> FirstOrdinalResults(this IDbCommand command)
		{
			var results = new Queue<object>(IterateReaderInternal(command, CommandBehavior.Default, r => r.GetValue(0)));
			return results.DequeueEach().DBNullToNull();
		}

		/// <summary>
		/// Reads the first column values from every record.
		/// Any DBNull values are then converted to null and casted to type T0;
		/// </summary>
		/// <returns>The enumerable of casted values.</returns>
		public static IEnumerable<T0> FirstOrdinalResults<T0>(this IDbCommand command)
			=> command.FirstOrdinalResults().Cast<T0>();

		/// <summary>
		/// Reads the first column values from every record.
		/// DBNull values are converted to null.
		/// </summary>
		/// <returns>The list of values.</returns>
		public static async Task<IEnumerable<object>> FirstOrdinalResultsAsync(this DbDataReader reader)
		{
			var results = new Queue<object>();
			await reader.ForEachAsync(r => results.Enqueue(r.GetValue(0)));
			return results.DequeueEach().DBNullToNull();
		}

		/// <summary>
		/// Reads the first column values from every record.
		/// Any DBNull values are then converted to null and casted to type T0;
		/// </summary>
		/// <returns>The enumerable of casted values.</returns>
		public static async Task<IEnumerable<T0>> FirstOrdinalResultsAsync<T0>(this DbDataReader reader)
		{
			var results = new Queue<object>();
			await reader.ForEachAsync(r => results.Enqueue(r.GetValue(0)));
			return results.DequeueEach().DBNullToNull().Cast<T0>(); ;
		}


		/// <summary>
		/// Reads the first column values from every record.
		/// DBNull values are converted to null.
		/// </summary>
		/// <returns>The list of values.</returns>
		public static async Task<IEnumerable<object>> FirstOrdinalResultsAsync(this DbCommand command)
		{
			var results = new Queue<object>();
			await command.ForEachAsync(r => results.Enqueue(r.GetValue(0)));
			return results.DequeueEach().DBNullToNull();
		}

		/// <summary>
		/// Reads the first column from every record..
		/// Any DBNull values are then converted to null and casted to type T0;
		/// </summary>
		/// <returns>The enumerable of casted values.</returns>
		public static async Task<IEnumerable<T0>> FirstOrdinalResultsAsync<T0>(this DbCommand command)
			=> (await command.FirstOrdinalResultsAsync()).Cast<T0>();

		/// <summary>
		/// Asynchronously returns all records and iteratively attempts to map the fields to type T.
		/// </summary>
		/// <typeparam name="T">The model type to map the values to (using reflection).</typeparam>
		/// <param name="reader">The IDataReader to read results from.</param>
		/// <param name="fieldMappingOverrides">An override map of field names to column names where the keys are the property names, and values are the column names.</param>
		/// <returns>A task containing the list of results.</returns>
		public static async Task<IEnumerable<T>> ResultsAsync<T>(this DbDataReader reader, IEnumerable<(string Field, string Column)> fieldMappingOverrides) where T : new()
		{
			if (!await reader.ReadAsync()) return Enumerable.Empty<T>();

			var x = new Transformer<T>(fieldMappingOverrides);
			// Ignore missing columns.
			var columns = reader.GetMatchingOrdinals(x.ColumnNames, true);

			return x.AsDequeueingEnumerable(await RetrieveAsyncInternal(reader,
				columns.Select(c => c.Ordinal).ToArray(),
				columns.Select(c => c.Name).ToArray(), readStarted: true));
		}

		/// <summary>
		/// Asynchronously returns all records and iteratively attempts to map the fields to type T.
		/// </summary>
		/// <typeparam name="T">The model type to map the values to (using reflection).</typeparam>
		/// <param name="reader">The IDataReader to read results from.</param>
		/// <param name="fieldMappingOverrides">An override map of field names to column names where the keys are the property names, and values are the column names.</param>
		/// <returns>A task containing the list of results.</returns>
		public static Task<IEnumerable<T>> ResultsAsync<T>(this DbDataReader reader, IEnumerable<KeyValuePair<string, string>> fieldMappingOverrides) where T : new()
			=> ResultsAsync<T>(reader, fieldMappingOverrides?.Select(kvp => (kvp.Key, kvp.Value)));

		/// <summary>
		/// Asynchronously returns all records and iteratively attempts to map the fields to type T.
		/// </summary>
		/// <typeparam name="T">The model type to map the values to (using reflection).</typeparam>
		/// <param name="reader">The IDataReader to read results from.</param>
		/// <param name="fieldMappingOverrides">An override map of field names to column names where the keys are the property names, and values are the column names.</param>
		/// <returns>A task containing the list of results.</returns>
		public static Task<IEnumerable<T>> ResultsAsync<T>(this DbDataReader reader, params (string Field, string Column)[] fieldMappingOverrides) where T : new()
			=> ResultsAsync<T>(reader, fieldMappingOverrides as IEnumerable<(string Field, string Column)>);

		/// <summary>
		/// Iterates each record and attempts to map the fields to type T.
		/// Data is temporarily stored (buffered in entirety) in a queue of dictionaries before applying the transform for each iteration.
		/// </summary>
		/// <typeparam name="T">The model type to map the values to (using reflection).</typeparam>
		/// <param name="reader">The IDataReader to read results from.</param>
		/// <param name="fieldMappingOverrides">An optional override map of field names to column names where the keys are the property names, and values are the column names.</param>
		/// <returns>The enumerable to pull the transformed results from.</returns>
		public static IEnumerable<T> Results<T>(this IDataReader reader, IEnumerable<(string Field, string Column)> fieldMappingOverrides)
			where T : new()
		{
			if (!reader.Read()) return Enumerable.Empty<T>();

			var x = new Transformer<T>(fieldMappingOverrides);
			// Ignore missing columns.
			var columns = reader.GetMatchingOrdinals(x.ColumnNames, true);

			return x.AsDequeueingEnumerable(RetrieveInternal(reader,
				columns.Select(c => c.Ordinal).ToArray(),
				columns.Select(c => c.Name).ToArray(), readStarted: true));
		}

		/// <summary>
		/// Iterates each record and attempts to map the fields to type T.
		/// Data is temporarily stored (buffered in entirety) in a queue of dictionaries before applying the transform for each iteration.
		/// </summary>
		/// <typeparam name="T">The model type to map the values to (using reflection).</typeparam>
		/// <param name="reader">The IDataReader to read results from.</param>
		/// <param name="fieldMappingOverrides">An optional override map of field names to column names where the keys are the property names, and values are the column names.</param>
		/// <returns>The enumerable to pull the transformed results from.</returns>
		public static IEnumerable<T> Results<T>(this IDataReader reader, params (string Field, string Column)[] fieldMappingOverrides)
			where T : new()
			=> Results<T>(reader, fieldMappingOverrides as IEnumerable<(string Field, string Column)>);

		/// <summary>
		/// Iterates each record and attempts to map the fields to type T.
		/// Data is temporarily stored (buffered in entirety) in a queue of dictionaries before applying the transform for each iteration.
		/// </summary>
		/// <typeparam name="T">The model type to map the values to (using reflection).</typeparam>
		/// <param name="reader">The IDataReader to read results from.</param>
		/// <param name="fieldMappingOverrides">An optional override map of field names to column names where the keys are the property names, and values are the column names.</param>
		/// <returns>The enumerable to pull the transformed results from.</returns>
		public static IEnumerable<T> Results<T>(this IDataReader reader, IEnumerable<KeyValuePair<string, string>> fieldMappingOverrides)
			where T : new()
			=> Results<T>(reader, fieldMappingOverrides?.Select(kvp => (kvp.Key, kvp.Value)));

		/// <summary>
		/// Iterates each record and attempts to map the fields to type T.
		/// Data is temporarily stored (buffered in entirety) in a queue of dictionaries before applying the transform for each iteration.
		/// </summary>
		/// <typeparam name="T">The model type to map the values to (using reflection).</typeparam>
		/// <param name="command">The command to generate a reader from.</param>
		/// <param name="fieldMappingOverrides">An optional override map of field names to column names where the keys are the property names, and values are the column names.</param>
		/// <returns>The enumerable to pull the transformed results from.</returns>
		public static IEnumerable<T> Results<T>(this IDbCommand command, IEnumerable<(string Field, string Column)> fieldMappingOverrides)
			where T : new()
			=> command.ExecuteReader(reader => reader.Results<T>(fieldMappingOverrides));

		/// <summary>
		/// Iterates each record and attempts to map the fields to type T.
		/// Data is temporarily stored (buffered in entirety) in a queue of dictionaries before applying the transform for each iteration.
		/// </summary>
		/// <typeparam name="T">The model type to map the values to (using reflection).</typeparam>
		/// <param name="command">The command to generate a reader from.</param>
		/// <param name="fieldMappingOverrides">An optional override map of field names to column names where the keys are the property names, and values are the column names.</param>
		/// <returns>The enumerable to pull the transformed results from.</returns>
		public static IEnumerable<T> Results<T>(this IDbCommand command, params (string Field, string Column)[] fieldMappingOverrides)
			where T : new()
			=> Results<T>(command, fieldMappingOverrides as IEnumerable<(string Field, string Column)>);

		/// <summary>
		/// Iterates each record and attempts to map the fields to type T.
		/// Data is temporarily stored (buffered in entirety) in a queue of dictionaries before applying the transform for each iteration.
		/// </summary>
		/// <typeparam name="T">The model type to map the values to (using reflection).</typeparam>
		/// <param name="command">The command to generate a reader from.</param>
		/// <param name="fieldMappingOverrides">An optional override map of field names to column names where the keys are the property names, and values are the column names.</param>
		/// <returns>The enumerable to pull the transformed results from.</returns>
		public static IEnumerable<T> Results<T>(this IDbCommand command, IEnumerable<KeyValuePair<string, string>> fieldMappingOverrides)
			where T : new()
			=> Results<T>(command, fieldMappingOverrides?.Select(kvp => (kvp.Key, kvp.Value)));


		// NOTE: The Results<T> methods should be faster than the ResultsFromDataTable<T> variations but are provided for validation of this assumption.

		/// <summary>
		/// Loads all data into a DataTable before Iterates each record and attempts to map the fields to type T.
		/// Data is temporarily stored (buffered in entirety) in a queue of dictionaries before applying the transform for each iteration.
		/// </summary>
		/// <typeparam name="T">The model type to map the values to (using reflection).</typeparam>
		/// <param name="reader">The IDataReader to read results from.</param>
		/// <param name="fieldMappingOverrides">An optional override map of field names to column names where the keys are the property names, and values are the column names.</param>
		/// <returns>The enumerable to pull the transformed results from.</returns>
		public static IEnumerable<T> ResultsFromDataTable<T>(this IDataReader reader, IEnumerable<KeyValuePair<string, string>> fieldMappingOverrides = null)
			where T : new()
			=> reader.ToDataTable().To<T>(fieldMappingOverrides, true);

		/// <summary>
		/// Loads all data into a DataTable before Iterates each record and attempts to map the fields to type T.
		/// Data is temporarily stored (buffered in entirety) in a queue of dictionaries before applying the transform for each iteration.
		/// </summary>
		/// <typeparam name="T">The model type to map the values to (using reflection).</typeparam>
		/// <param name="command">The command to generate a reader from.</param>
		/// <param name="fieldMappingOverrides">An optional override map of field names to column names where the keys are the property names, and values are the column names.</param>
		/// <returns>The enumerable to pull the transformed results from.</returns>
		public static IEnumerable<T> ResultsFromDataTable<T>(this IDbCommand command, IEnumerable<KeyValuePair<string, string>> fieldMappingOverrides = null)
			where T : new()
			=> command.ToDataTable().To<T>(fieldMappingOverrides, true);

		/// <summary>
		/// Iterates an IDataReader through the transform function and posts each record to the target block.
		/// </summary>
		/// <typeparam name="T">The return type of the transform function.</typeparam>
		/// <param name="reader">The IDataReader to iterate.</param>
		/// <param name="transform">The transform function for each IDataRecord.</param>
		/// <param name="target">The target block to receivethe results.</param>
		public static void ToTargetBlock<T>(this IDataReader reader,
			ITargetBlock<T> target,
			Func<IDataRecord, T> transform)
		{
			while (target.IsStillAlive() && reader.Read() && target.Post(transform(reader))) { }
		}

		/// <summary>
		/// Asynchronously iterates an IDataReader and through the transform function and posts each record it to the target block.
		/// </summary>
		/// <typeparam name="T">The return type of the transform function.</typeparam>
		/// <param name="reader">The SqlDataReader to read from.</param>
		/// <param name="target">The target block to receive the results.</param>
		/// <param name="transform">The transform function to process each IDataRecord.</param>
		public static async Task ToTargetBlockAsync<T>(this DbDataReader reader,
			ITargetBlock<T> target,
			Func<IDataRecord, T> transform)
		{
			Task<bool> lastSend = null;
			while (target.IsStillAlive()
				&& await reader.ReadAsync()
				&& (lastSend == null || await lastSend))
			{
				// Allows for a premtive read before waiting for the next send.
				lastSend = target.SendAsync(transform(reader));
			}
		}

		/// <summary>
		/// Asynchronously iterates an IDataReader and through the transform function and posts each record it to the target block.
		/// </summary>
		/// <typeparam name="T">The return type of the transform function.</typeparam>
		/// <param name="command">The DbCommand to generate a reader from.</param>
		/// <param name="target">The target block to receive the results.</param>
		/// <param name="transform">The transform function for each IDataRecord.</param>
		public static async Task ToTargetBlockAsync<T>(this DbCommand command,
			ITargetBlock<T> target,
			Func<IDataRecord, T> transform)
		{
			if (target.IsStillAlive())
			{
				using (var reader = await command.ExecuteReaderAsync())
				{
					if (target.IsStillAlive())
						await reader.ToTargetBlockAsync(target, transform);
				}
			}
		}

		/// <summary>
		/// Iterates an IDataReader through the transform function and posts each record to the target block.
		/// </summary>
		/// <typeparam name="T">The return type of the transform function.</typeparam>
		/// <param name="command">The IDataReader to iterate.</param>
		/// <param name="target">The target block to receive the results.</param>
		/// <param name="transform">The transform function for each IDataRecord.</param>
		public static void ToTargetBlock<T>(this IDbCommand command,
			ITargetBlock<T> target,
			Func<IDataRecord, T> transform)
			=> command.ExecuteReader(reader => reader.ToTargetBlock(target, transform));

		/// <summary>
		/// Creates an ExpressiveDbCommand for subsequent configuration and execution.
		/// </summary>
		/// <param name="target">The connection factory to generate a commands from.</param>
		/// <param name="command">The command text or stored procedure name to use.</param>
		/// <param name="type">The command type.</param>
		/// <returns>The resultant ExpressiveDbCommand.</returns>
		public static ExpressiveDbCommand Command(
			this IDbConnectionFactory<DbConnection> target,
			string command,
			CommandType type = CommandType.Text)
			=> new ExpressiveDbCommand(target, type, command);

		/// <summary>
		/// Creates an ExpressiveDbCommand with command type set to StoredProcedure for subsequent configuration and execution.
		/// </summary>
		/// <param name="target">The connection factory to generate a commands from.</param>
		/// <param name="command">The command text or stored procedure name to use.</param>
		/// <returns>The resultant ExpressiveDbCommand.</returns>
		public static ExpressiveDbCommand StoredProcedure(
			this IDbConnectionFactory<DbConnection> target,
			string command)
			=> new ExpressiveDbCommand(target, CommandType.StoredProcedure, command);

		/// <summary>
		/// Creates an ExpressiveDbCommand for subsequent configuration and execution.
		/// </summary>
		/// <param name="target">The connection factory to generate a commands from.</param>
		/// <param name="command">The command text or stored procedure name to use.</param>
		/// <param name="type">The command type.</param>
		/// <returns>The resultant ExpressiveDbCommand.</returns>
		public static ExpressiveDbCommand Command(
			this Func<DbConnection> target,
			string command,
			CommandType type = CommandType.Text)
			=> Command(new DbConnectionFactory(target), command, type);

		/// <summary>
		/// Creates an ExpressiveDbCommand with command type set to StoredProcedure for subsequent configuration and execution.
		/// </summary>
		/// <param name="target">The connection factory to generate a commands from.</param>
		/// <param name="command">The command text or stored procedure name to use.</param>
		/// <returns>The resultant ExpressiveDbCommand.</returns>
		public static ExpressiveDbCommand StoredProcedure(
			this Func<DbConnection> target,
			string command)
			=> StoredProcedure(new DbConnectionFactory(target), command);
	}
}
