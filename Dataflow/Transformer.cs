﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
// ReSharper disable MemberCanBePrivate.Local
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedMember.Global

namespace Open.Database.Extensions.Core
{
	public class Transformer<T>
		where T : new()
	{
		public readonly Type Type;
		public readonly PropertyInfo[] Properties;

		// Allow mapping key = object property, value = column name.
		readonly Dictionary<string, string> PropertyMap;
		readonly Dictionary<string, PropertyInfo> ColumnToPropertyMap;
		public IEnumerable<string> PropertyNames => PropertyMap.Keys;
		public IEnumerable<string> ColumnNames => PropertyMap.Values;

		public Transformer(IEnumerable<(string Field, string Column)>? overrides = null)
		{
			Type = typeof(T);
			Properties = Type.GetProperties();
			PropertyMap = Properties.Select(p => p.Name).ToDictionary(n => n);

			var pm = Properties.ToDictionary(p => p.Name);

			if (overrides != null)
			{
				foreach (var (Field, Column) in overrides)
				{
					var cn = Column;
					if (cn == null) PropertyMap.Remove(Field); // Null values indicate a desire to 'ignore' a field.
					else PropertyMap[Field] = cn;
				}
			}

			ColumnToPropertyMap = PropertyMap.ToDictionary(kvp => kvp.Value.ToLowerInvariant(), kvp => pm[kvp.Key]);

		}

		class Processor
		{
			public Processor(Transformer<T> transformer, IList<string>? names = null)
			{
				Transformer = transformer;
				Transform = record =>
				{
					var model = new T();
					var count = _names.Count;
					for (var i = 0; i < count; i++)
					{
						var p = _propertySetters[i];
						if (p != null)
						{
							object? value = record[i];
							if (value == DBNull.Value) value = null;
							try
							{
								p(model, value);
							}
							catch (Exception ex)
							{
								throw new InvalidOperationException($"Unable to set value of property '{_names[i]}'.", ex);
							}
						}
					}

					return model;
				};

				if (names != null) SetNames(names);
			}

			public readonly Transformer<T> Transformer;

            IList<string> _names = Array.Empty<string>();
			Action<T, object?>?[] _propertySetters = Array.Empty<Action<T, object?>?>();

			public readonly Func<object[], T> Transform; // Using a Func<object[],T> for better type inference.

			public void SetNames(IList<string> names)
			{
				var map = Transformer.ColumnToPropertyMap;
				_names = names;
				_propertySetters = names
					.Select(n => map.TryGetValue(n.ToLowerInvariant(), out var p) ? p.BuildUntypedSetter<T>() : null)
					.ToArray();
			}

			public TransformBlock<object[], T> GetBlock(
				ExecutionDataflowBlockOptions? options = null)
				=> options==null
					? new TransformBlock<object[], T>(Transform)
					: new TransformBlock<object[], T>(Transform, options);
		}

		public IEnumerable<T> AsDequeueingEnumerable(QueryResult<Queue<object[]>> results)
		{
			var processor = new Processor(this, results.Names);
			var q = results.Result;

			while (q.Count != 0)
				yield return processor.Transform(q.Dequeue());

			// By using the above routine, we guarantee as enumeration occurs, references are released (dequeued).
		}

		public IReceivableSourceBlock<T> Results(
			out Action<QueryResult<IEnumerable<object[]>>> deferred,
			ExecutionDataflowBlockOptions? options = null)
		{
			var processor = new Processor(this);
			var x = processor.GetBlock(options);

			deferred = results =>
			{
				processor.SetNames(results.Names);
				var q = results.Result;
				foreach (var record in q) if (!x.Post(record)) break;
				x.Complete(); // May not be necessary, but we'll call it to ensure the .Completion occurs.
			};

			return x;
		}

		public IReceivableSourceBlock<T> ResultsAsync(
			out Func<QueryResult<IEnumerable<object[]>>, ValueTask> deferred,
			ExecutionDataflowBlockOptions? options = null)
		{
			var processor = new Processor(this);
			var x = processor.GetBlock(options);

			deferred = async results =>
			{
				processor.SetNames(results.Names);
				var q = results.Result;
				foreach (var record in q) if (!await x.SendAsync(record)) break;
				x.Complete(); // May not be necessary, but we'll call it to ensure the .Completion occurs.
			};

			return x;
		}

		public IReceivableSourceBlock<T> Results(
			QueryResult<IReceivableSourceBlock<object[]>> source,
			ExecutionDataflowBlockOptions? options = null)
		{
			var processor = new Processor(this, source.Names);
			var x = processor.GetBlock(options);
			var r = source.Result;
			r.LinkTo(x, new DataflowLinkOptions { PropagateCompletion = true });
			x.Completion.ContinueWith(t => r.Complete(), TaskContinuationOptions.ExecuteSynchronously); // Signal that no more results can be received.
			return x;
		}

		public TransformBlock<object[], T> ResultsBlock(
			out Action<string[]> initColumnNames,
			ExecutionDataflowBlockOptions? options = null)
		{
			var processor = new Processor(this);
			var x = processor.GetBlock(options);

			initColumnNames = results => processor.SetNames(results);

			return x;
		}

		public IEnumerable<T> Results(DataTable table, bool clearTable)
		{
			var columns = table.Columns.AsEnumerable();
			var results = new QueryResult<Queue<object[]>>(
				columns.Select(c => c.Ordinal),
				columns.Select(c => c.ColumnName),
				new Queue<object[]>(table.Rows.AsEnumerable().Select(r => r.ItemArray)));
			if (clearTable) table.Rows.Clear();
			return AsDequeueingEnumerable(results);
		}

	}
}